package org.miracum.recruit.querysqlonfhir;

import ca.uhn.fhir.model.api.Include;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.gclient.ReferenceClientParam;
import ca.uhn.fhir.util.BundleUtil;
import java.util.Date;
import java.util.Optional;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Group;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.miracum.recruit.querysqlonfhir.config.FhirSystems;
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
  private final FhirSystems fhirSystems;

  public PollForStudies(
      SqlQueryExecutor sqlQueryExecutor,
      EligibilityBundleBuilder bundleBuilder,
      IGenericClient fhirClient,
      FhirSystems fhirSystems) {
    this.sqlQueryExecutor = sqlQueryExecutor;
    this.bundleBuilder = bundleBuilder;
    this.fhirClient = fhirClient;
    this.fhirSystems = fhirSystems;
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

    var results = sqlQueryExecutor.evaluateEligibility(criteria);

    log.info(
        "{} patients are candidates or have unknown eligibility for study {}",
        results.size(),
        study.getId());

    var effectiveDate = new Date();

    var subjectAndObservationBundles =
        bundleBuilder.buildSubjectAndObservationBundles(study, results, effectiveDate);
    for (var bundle : subjectAndObservationBundles) {
      fhirClient.transaction().withBundle(bundle).execute();
    }

    // built and submitted after every chunk above has committed, so its conditional
    // ResearchSubject references (see EligibilityBundleBuilder) actually resolve.
    var previousScreeningList = fetchPreviousScreeningList(study);
    var listBundle = bundleBuilder.buildScreeningListBundle(study, results, previousScreeningList);
    fhirClient.transaction().withBundle(listBundle).execute();
  }

  private Optional<ListResource> fetchPreviousScreeningList(ResearchStudy study) {
    var identifierSystem = fhirSystems.screeningListIdentifier();
    var identifierValue = study.getIdentifierFirstRep().getValue();

    var previousListBundle =
        fhirClient
            .search()
            .forResource(ListResource.class)
            .where(
                ListResource.IDENTIFIER
                    .exactly()
                    .systemAndIdentifier(identifierSystem, identifierValue))
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
          "Found more than one List resource matching identifier " + identifierValue);
    }

    return Optional.of(listResources.getFirst());
  }
}
