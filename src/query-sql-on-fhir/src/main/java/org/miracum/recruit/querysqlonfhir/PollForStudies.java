package org.miracum.recruit.querysqlonfhir;

import ca.uhn.fhir.model.api.Include;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.gclient.ReferenceClientParam;
import ca.uhn.fhir.util.BundleUtil;
import io.github.miracum.recruit.Recruit;
import java.util.ArrayList;
import java.util.Date;
import java.util.HashSet;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import org.hl7.fhir.instance.model.api.IBaseBundle;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Group;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.hl7.fhir.r4.model.ResearchSubject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

@Component
public class PollForStudies {

  private static final Logger log = LoggerFactory.getLogger(PollForStudies.class);

  private static final String SQL_QUERY_LIBRARY_TYPE_SYSTEM =
      "https://sql-on-fhir.org/ig/CodeSystem/LibraryTypesCodes";
  private static final String SQL_QUERY_LIBRARY_TYPE_CODE = "sql-query";

  private final SqlQueryExecutor sqlQueryExecutor;
  private final EligibilityBundleBuilder bundleBuilder;
  private final IGenericClient fhirClient;

  public PollForStudies(
      SqlQueryExecutor sqlQueryExecutor,
      EligibilityBundleBuilder bundleBuilder,
      IGenericClient fhirClient) {
    this.sqlQueryExecutor = sqlQueryExecutor;
    this.bundleBuilder = bundleBuilder;
    this.fhirClient = fhirClient;
  }

  /**
   * Runs a single poll cycle: finds all matching ResearchStudy resources and processes each one,
   * isolating individual failures so one bad study doesn't stop the rest. Called on a schedule by
   * {@link ScheduledPolling} or once by {@link OneShotRunner}, depending on {@code
   * query-sql-on-fhir.run-mode} - the isolation behavior below is identical either way, only what
   * happens with the returned failure count differs.
   */
  public PollResult pollForStudies() {
    log.info(
        "Polling for ResearchStudy resources referencing a Group with SQLQuery Library"
            + " characteristics");

    var studyBundle =
        fhirClient
            .search()
            .forResource(ResearchStudy.class)
            .where(
                new ReferenceClientParam("enrollment")
                    .hasChainedProperty(
                        "Group",
                        new ReferenceClientParam("characteristic")
                            .hasChainedProperty(
                                "Library",
                                Library.TYPE
                                    .exactly()
                                    .systemAndCode(
                                        SQL_QUERY_LIBRARY_TYPE_SYSTEM,
                                        SQL_QUERY_LIBRARY_TYPE_CODE))))
            .include(new Include("ResearchStudy:enrollment"))
            .include(new Include("Group:characteristic", true))
            .withAdditionalHeader("Prefer", "handling=strict")
            .returnBundle(Bundle.class)
            .encodedJson()
            .execute();

    var researchStudies =
        BundleUtil.toListOfResourcesOfType(
            fhirClient.getFhirContext(), studyBundle, ResearchStudy.class);

    var failedCount = 0;
    for (var study : researchStudies) {
      try {
        processStudy(study);
      } catch (Exception ex) {
        // isolate one study's failure so it doesn't abort polling for every other study found in
        // this run - now that a study's update can span several chunked transactions, a mid-way
        // failure is no longer necessarily atomic the way a single-transaction update was.
        failedCount++;
        log.error("Failed to process ResearchStudy with id {}", study.getId(), ex);
      }
    }

    return new PollResult(researchStudies.size(), failedCount);
  }

  private void processStudy(ResearchStudy study) {
    log.info("Found ResearchStudy with id: {}", study.getId());

    var group = (Group) study.getEnrollmentFirstRep().getResource();
    log.info(
        "Found Group with id: {}, {}", group.getId(), group.getIdentifierFirstRep().getValue());

    var criteria =
        group.getCharacteristic().stream()
            .map(
                c ->
                    new EligibilityCriterion(
                        (Library) c.getValueReference().getResource(),
                        c.getCode().getText(),
                        c.getExclude()))
            .toList();

    log.info("Found {} eligibility criteria for study {}", criteria.size(), study.getId());

    var effectiveDate = new Date();

    // Only needed in update-as-create mode: without it, POST's conditional-create already
    // guarantees existing ResearchSubjects are left untouched (see EligibilityBundleBuilder).
    Set<String> existingResearchSubjectIds;
    if (bundleBuilder.usesUpdateAsCreate()) {
      existingResearchSubjectIds = fetchExistingResearchSubjectIds(study);
    } else {
      existingResearchSubjectIds = Set.of();
    }

    // Results are streamed in and submitted per chunkSize-sized batch as they arrive (see
    // SqlQueryExecutor#evaluateEligibility), rather than the whole study's candidate population
    // ever being held in memory at once - only patientIds accumulates for the full population,
    // which is cheap compared to each patient's full per-criterion outcome details.
    var patientIds = new ArrayList<String>();
    var batch = new ArrayList<PatientEligibilityResult>(bundleBuilder.chunkSize());
    sqlQueryExecutor.evaluateEligibility(
        criteria,
        result -> {
          patientIds.add(result.patientId());
          batch.add(result);
          if (batch.size() >= bundleBuilder.chunkSize()) {
            submitSubjectAndObservationBatch(
                study, batch, effectiveDate, existingResearchSubjectIds);
            batch.clear();
          }
        });
    if (!batch.isEmpty()) {
      submitSubjectAndObservationBatch(study, batch, effectiveDate, existingResearchSubjectIds);
    }

    log.info(
        "{} patients are candidates or have unknown eligibility for study {}",
        patientIds.size(),
        study.getId());

    // built and submitted after every batch above has committed, so its conditional
    // ResearchSubject references (see EligibilityBundleBuilder) actually resolve.
    var previousScreeningList = fetchPreviousScreeningList(study);
    var listBundle =
        bundleBuilder.buildScreeningListBundle(study, patientIds, previousScreeningList);
    fhirClient.transaction().withBundle(listBundle).execute();
  }

  private void submitSubjectAndObservationBatch(
      ResearchStudy study,
      List<PatientEligibilityResult> batch,
      Date effectiveDate,
      Set<String> existingResearchSubjectIds) {
    var bundle =
        bundleBuilder.buildSubjectAndObservationBundle(
            study, batch, effectiveDate, existingResearchSubjectIds);
    fhirClient.transaction().withBundle(bundle).execute();
  }

  private Optional<ListResource> fetchPreviousScreeningList(ResearchStudy study) {
    var identifierSystem = Recruit.NamingSystems.ScreeningListId.uri();
    var identifierValue = study.getIdentifierFirstRep().getValue();

    var previousListBundle =
        fhirClient
            .search()
            .forResource(ListResource.class)
            .where(
                ListResource.IDENTIFIER
                    .exactly()
                    .systemAndIdentifier(identifierSystem, identifierValue))
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
          "Found more than one List resource matching identifier " + identifierValue);
    }

    return Optional.of(listResources.getFirst());
  }

  /**
   * Ids of every ResearchSubject already on the server for this study - only called in
   * update-as-create mode, to keep {@link EligibilityBundleBuilder} from PUTting over (and thereby
   * resetting the status of) a subject list-next has since updated. Paginates through the full
   * result set since a study can have far more subjects than fit on one page.
   */
  private Set<String> fetchExistingResearchSubjectIds(ResearchStudy study) {
    var studyReference = "ResearchStudy/" + study.getIdElement().getIdPart();

    var ids = new HashSet<String>();
    var page =
        fhirClient
            .search()
            .forResource(ResearchSubject.class)
            .where(ResearchSubject.STUDY.hasId(studyReference))
            .returnBundle(Bundle.class)
            .encodedJson()
            .execute();

    while (page != null) {
      BundleUtil.toListOfResourcesOfType(fhirClient.getFhirContext(), page, ResearchSubject.class)
          .forEach(subject -> ids.add(subject.getIdElement().getIdPart()));

      if (page.getLink(IBaseBundle.LINK_NEXT) != null) {
        page = fhirClient.loadPage().next(page).execute();
      } else {
        page = null;
      }
    }

    return ids;
  }
}
