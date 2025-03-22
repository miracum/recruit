package org.miracum.recruit.queryfhirtrino;

import ca.uhn.fhir.model.api.Include;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.gclient.ReferenceClientParam;
import java.nio.charset.StandardCharsets;
import java.text.SimpleDateFormat;
import java.util.Base64;
import java.util.Date;
import java.util.List;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Bundle.BundleEntryRequestComponent;
import org.hl7.fhir.r4.model.Group;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.ListResource.ListStatus;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.hl7.fhir.r4.model.ResearchSubject;
import org.hl7.fhir.r4.model.ResourceType;
import org.miracum.recruit.queryfhirtrino.config.FhirSystems;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

@Component
public class PollForStudies {

  private static final Logger log = LoggerFactory.getLogger(PollForStudies.class);

  private static final SimpleDateFormat dateFormat = new SimpleDateFormat("HH:mm:ss");

  private JdbcTemplate jdbcTemplate;

  private IGenericClient fhirClient;

  private FhirSystems fhirSystems;

  public PollForStudies(
      JdbcTemplate jdbcTemplate, IGenericClient fhirClient, FhirSystems fhirSystems) {
    this.jdbcTemplate = jdbcTemplate;
    this.fhirClient = fhirClient;
    this.fhirSystems = fhirSystems;
  }

  @Scheduled(fixedRate = 5000)
  public void pollForStudies() {
    log.info("The time is now {}", dateFormat.format(new Date()));
    // 1. get all FHIR ResearchStudy resources from the fhir server:
    //
    // http://recruit-fhir-server.127.0.0.1.nip.io/fhir/ResearchStudy?enrollment.code=trino-sql-encoded-eligibility-criteria&_include=ResearchStudy:enrollment&_include:iterate=Group:characteristic-reference
    // 2. for each ResearchStudy resource, get the associated FHIR Group resource, and the
    // assoaciated FHIR Library resource
    // 3. for each Library resource, fetch the SQL query
    // 4. finally, run this sql query against trino
    // 5. for each returned patient id/row, create a new FHIR ResearchSubject resource, referencing
    // the patient_id and the original ResearchStudy from above
    // 6. post a FHIR List referencing all the ResearchSubject resources

    var studyBundle =
        fhirClient
            .search()
            .forResource(ResearchStudy.class)
            .where(
                new ReferenceClientParam("enrollment")
                    .hasChainedProperty(
                        "Group",
                        Group.CODE
                            .exactly()
                            .systemAndCode(
                                "https://miracum.github.io/recruit/fhir/CodeSystem/eligibility-criteria-types",
                                "trino-sql-encoded-eligibility-criteria")))
            .include(new Include("ResearchStudy:enrollment"))
            .include(new Include("Group:characteristic-reference", true))
            .returnBundle(Bundle.class)
            .execute();

    for (var entry : studyBundle.getEntry()) {
      var resource = entry.getResource();
      if (resource instanceof ResearchStudy study) {
        log.info("Found ResearchStudy with id: {}", study.getId());

        var group = (Group) study.getEnrollmentFirstRep().getResource();

        log.info(
            "Found Group with id: {}, {}", group.getId(), group.getIdentifierFirstRep().getValue());

        var library = (Library) group.getCharacteristicFirstRep().getValueReference().getResource();

        byte[] decoded =
            Base64.getDecoder()
                .decode(library.getContentFirstRep().getDataElement().asStringValue());

        var contentString = new String(decoded, StandardCharsets.UTF_8);

        log.info("Found Library with id: {}, {}", library.getId(), contentString);

        var result = jdbcTemplate.queryForList(contentString);

        for (var row : result) {
          log.info("Found patient with id: {}", row.get("patient_id"));
        }

        var bundle =
            createScreeningListBundle(
                study, result.stream().map(r -> (String) r.get("patient_id")).toList());

        var str =
            fhirClient
                .getFhirContext()
                .newJsonParser()
                .setPrettyPrint(true)
                .encodeResourceToString(bundle);
        log.info("Bundle: {}", str);

        fhirClient.transaction().withBundle(bundle).execute();
      }
    }
  }

  private Bundle createScreeningListBundle(ResearchStudy study, List<String> patientIds) {
    var bundle = new Bundle();
    bundle.setType(Bundle.BundleType.TRANSACTION);
    bundle.setTimestamp(new Date());

    var screeningList =
        new ListResource().setStatus(ListStatus.CURRENT).setMode(ListResource.ListMode.WORKING);
    screeningList
        .addIdentifier()
        .setSystem(fhirSystems.screeningListIdentifier())
        .setValue(study.getIdentifierFirstRep().getValue());

    for (var patientId : patientIds) {
      var subject =
          new ResearchSubject()
              .setStudy(new Reference("ResearchStudy/" + study.getIdElement().getIdPart()))
              .setIndividual(new Reference("Patient/" + patientId))
              .setStatus(ResearchSubject.ResearchSubjectStatus.CANDIDATE);

      var subjectEntry =
          new Bundle.BundleEntryComponent()
              .setResource(subject)
              .setFullUrl(IdType.newRandomUuid().getValue())
              .setRequest(
                  new BundleEntryRequestComponent()
                      .setMethod(Bundle.HTTPVerb.POST)
                      .setIfNoneExist(
                          "ResearchSubject?patient=Patient/"
                              + patientId
                              + "&study=ResearchStudy/"
                              + study.getIdElement().getIdPart())
                      .setUrl(ResourceType.ResearchSubject.name()));

      bundle.addEntry(subjectEntry);
      screeningList.addEntry().setItem(new Reference(subjectEntry.getFullUrl()));
    }

    bundle
        .addEntry()
        .setResource(screeningList)
        .setFullUrl(IdType.newRandomUuid().getValue())
        .setRequest(
            new BundleEntryRequestComponent()
                .setMethod(Bundle.HTTPVerb.PUT)
                .setUrl(
                    "List?identifier="
                        + fhirSystems.screeningListIdentifier()
                        + "|"
                        + study.getIdentifierFirstRep().getValue()));

    return bundle;
  }
}
