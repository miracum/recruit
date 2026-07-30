package org.miracum.recruit.queryfhirtrino;

import ca.uhn.fhir.model.api.Include;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.gclient.ReferenceClientParam;
import ca.uhn.fhir.util.BundleUtil;
import com.google.common.hash.Hashing;
import java.nio.charset.StandardCharsets;
import java.util.Date;
import java.util.List;
import java.util.Optional;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Bundle.BundleEntryRequestComponent;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.Group;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.Identifier;
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
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

@Component
public class PollForStudies {

  private static final Logger log = LoggerFactory.getLogger(PollForStudies.class);

  private static final String SQL_QUERY_LIBRARY_TYPE_SYSTEM =
      "https://sql-on-fhir.org/ig/CodeSystem/LibraryTypesCodes";
  private static final String SQL_QUERY_LIBRARY_TYPE_CODE = "sql-query";

  private SqlQueryExecutor sqlQueryExecutor;
  private IGenericClient fhirClient;
  private FhirSystems fhirSystems;

  @Value("${fhir.use-upsert-instead-of-conditional-update}")
  private boolean useUpsertInsteadOfConditionalUpdate = false;

  public PollForStudies(
      SqlQueryExecutor sqlQueryExecutor, IGenericClient fhirClient, FhirSystems fhirSystems) {
    this.sqlQueryExecutor = sqlQueryExecutor;
    this.fhirClient = fhirClient;
    this.fhirSystems = fhirSystems;
  }

  @Scheduled(cron = "${query-fhir-trino.schedule.cron}")
  public void pollForStudies() {
    log.info(
        "Polling for ResearchStudy resources referencing a Group with a SQLQuery Library"
            + " characteristic");

    var studyBundle =
        fhirClient
            .search()
            .forResource(ResearchStudy.class)
            .where(
                new ReferenceClientParam("enrollment")
                    .hasChainedProperty(
                        "Group",
                        new ReferenceClientParam("characteristic-reference")
                            .hasChainedProperty(
                                "Library",
                                Library.TYPE
                                    .exactly()
                                    .systemAndCode(
                                        SQL_QUERY_LIBRARY_TYPE_SYSTEM,
                                        SQL_QUERY_LIBRARY_TYPE_CODE))))
            .include(new Include("ResearchStudy:enrollment"))
            .include(new Include("Group:characteristic-reference", true))
            .withAdditionalHeader("Prefer", "handling=strict")
            .returnBundle(Bundle.class)
            .encodedJson()
            .execute();

    var researchStudies =
        BundleUtil.toListOfResourcesOfType(
            fhirClient.getFhirContext(), studyBundle, ResearchStudy.class);

    for (var study : researchStudies) {
      log.info("Found ResearchStudy with id: {}", study.getId());

      var group = (Group) study.getEnrollmentFirstRep().getResource();

      log.info(
          "Found Group with id: {}, {}", group.getId(), group.getIdentifierFirstRep().getValue());

      var library = (Library) group.getCharacteristicFirstRep().getValueReference().getResource();

      log.info("Found SQLQuery Library with id: {}", library.getId());

      var patientIds = sqlQueryExecutor.resolvePatientIds(library);

      for (var patientId : patientIds) {
        log.info("Found patient with id: {}", patientId);
      }

      var bundle = createScreeningListBundle(study, patientIds);

      fhirClient.transaction().withBundle(bundle).execute();
    }
  }

  private Bundle createScreeningListBundle(ResearchStudy study, List<String> patientIds) {
    var bundle = new Bundle();
    bundle.setType(Bundle.BundleType.TRANSACTION);
    bundle.setTimestamp(new Date());

    var researchStudyReference = new Reference("ResearchStudy/" + study.getIdElement().getIdPart());

    var screeningListCode = new CodeableConcept();
    screeningListCode
        .addCoding()
        .setSystem(fhirSystems.screeningListCodeSystem())
        .setCode("screening-recommendations");
    var screeningList =
        new ListResource()
            .setStatus(ListStatus.CURRENT)
            .setMode(ListResource.ListMode.WORKING)
            .setCode(screeningListCode);
    screeningList
        .addIdentifier()
        .setSystem(fhirSystems.screeningListIdentifier())
        .setValue(study.getIdentifierFirstRep().getValue());
    screeningList
        .addExtension()
        .setUrl(fhirSystems.screeningListStudyReferenceExtension())
        .setValue(researchStudyReference);

    // fetch previous screening list to retain List.entry.date
    var previousScreeningList = fetchPreviousScreeningList(screeningList.getIdentifierFirstRep());

    for (var patientId : patientIds) {
      var subject =
          new ResearchSubject()
              .setStudy(researchStudyReference)
              .setIndividual(new Reference("Patient/" + patientId))
              .setStatus(ResearchSubject.ResearchSubjectStatus.CANDIDATE);

      var entryRequestComponent = new BundleEntryRequestComponent();
      if (useUpsertInsteadOfConditionalUpdate) {
        var idValue =
            "ResearchSubject?patient=Patient/"
                + patientId
                + "&study=ResearchStudy/"
                + study.getIdElement().getIdPart();
        var resourceId = Hashing.sha256().hashString(idValue, StandardCharsets.UTF_8).toString();
        subject.setId(resourceId);
        entryRequestComponent
            .setMethod(Bundle.HTTPVerb.PUT)
            .setUrl(ResourceType.ResearchSubject.name() + "/" + resourceId);
      } else {
        entryRequestComponent
            .setMethod(Bundle.HTTPVerb.POST)
            .setIfNoneExist(
                "ResearchSubject?patient=Patient/"
                    + patientId
                    + "&study=ResearchStudy/"
                    + study.getIdElement().getIdPart())
            .setUrl(ResourceType.ResearchSubject.name());
      }

      var subjectEntry =
          new Bundle.BundleEntryComponent()
              .setResource(subject)
              .setFullUrl(IdType.newRandomUuid().getValue())
              .setRequest(entryRequestComponent);

      bundle.addEntry(subjectEntry);

      // by default, the ListEntry date is the current date
      var listEntry =
          new ListResource.ListEntryComponent()
              .setItem(new Reference(subjectEntry.getFullUrl()))
              .setDate(new Date());

      if (previousScreeningList.isPresent()) {
        // check if there is an entry in the list with the same patient and study reference,
        // i.e. the same ResearchSubject
        var previousEntry =
            previousScreeningList.get().getEntry().stream()
                .filter(
                    item ->
                        ((ResearchSubject) item.getItem().getResource())
                                .getIndividual()
                                .getReference()
                                .equals(subject.getIndividual().getReference())
                            && ((ResearchSubject) item.getItem().getResource())
                                .getStudy()
                                .getReference()
                                .equals(subject.getStudy().getReference()))
                .findFirst();

        if (previousEntry.isPresent() && previousEntry.get().hasDate()) {
          listEntry.setDate(previousEntry.get().getDate());
        }
      }
      screeningList.addEntry(listEntry);
    }

    var entryRequestComponent = new BundleEntryRequestComponent();
    if (useUpsertInsteadOfConditionalUpdate) {
      var identifierValue =
          fhirSystems.screeningListIdentifier() + "|" + study.getIdentifierFirstRep().getValue();
      var resourceId =
          Hashing.sha256().hashString(identifierValue, StandardCharsets.UTF_8).toString();
      screeningList.setId(resourceId);
      entryRequestComponent
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(ResourceType.List.name() + "/" + resourceId);

    } else {
      entryRequestComponent
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(
              "List?identifier="
                  + fhirSystems.screeningListIdentifier()
                  + "|"
                  + study.getIdentifierFirstRep().getValue());
    }

    bundle
        .addEntry()
        .setResource(screeningList)
        .setFullUrl(IdType.newRandomUuid().getValue())
        .setRequest(entryRequestComponent);

    return bundle;
  }

  private Optional<ListResource> fetchPreviousScreeningList(Identifier identifier) {
    var previousListBundle =
        fhirClient
            .search()
            .forResource(ListResource.class)
            .where(
                ListResource.IDENTIFIER
                    .exactly()
                    .systemAndIdentifier(identifier.getSystem(), identifier.getValue()))
            .include(new Include("List:item"))
            .returnBundle(Bundle.class)
            .encodedJson()
            .withAdditionalHeader("Prefer", "handling=strict")
            .execute();

    var listResources =
        BundleUtil.toListOfResourcesOfType(
            fhirClient.getFhirContext(), previousListBundle, ListResource.class);

    if (listResources.isEmpty()) {
      return Optional.empty();
    }
    if (listResources.size() > 1) {
      throw new IllegalArgumentException(
          "Found more than one List resource matching identifier " + identifier.getValue());
    }

    return Optional.of(listResources.getFirst());
  }
}
