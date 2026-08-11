package org.miracum.recruit.querysqlonfhir;

import com.google.common.hash.Hashing;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Optional;
import org.hl7.fhir.r4.model.BooleanType;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Bundle.BundleEntryRequestComponent;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.Coding;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.ListResource.ListStatus;
import org.hl7.fhir.r4.model.Observation;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.hl7.fhir.r4.model.ResearchSubject;
import org.hl7.fhir.r4.model.ResourceType;
import org.miracum.recruit.querysqlonfhir.config.FhirSystems;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * Builds the FHIR transaction bundles {@link PollForStudies} submits from a study's merged
 * eligibility results: one or more chunked bundles of ResearchSubject + per-criterion Observation
 * entries (see {@link #buildSubjectAndObservationBundles}), and a separate screening List bundle
 * (see {@link #buildScreeningListBundle}) that must be submitted afterwards.
 *
 * <p>The List references each patient's ResearchSubject via a <a
 * href="https://www.hl7.org/fhir/http.html#trules">conditional reference</a> (a search URL, not a
 * bundle-internal {@code fullUrl}) precisely so it can be built and submitted independently of -
 * and after - whichever chunk actually created or updated that ResearchSubject; the server resolves
 * it against whatever now matches on the server, without this class needing to track
 * server-assigned ids across chunk submissions.
 */
@Component
public class EligibilityBundleBuilder {

  private static final String DATA_ABSENT_REASON_SYSTEM =
      "http://terminology.hl7.org/CodeSystem/data-absent-reason";
  private static final String DATA_ABSENT_REASON_UNKNOWN = "unknown";
  private static final String ELIGIBILITY_ASSESSMENT_CODE = "eligibility-assessment";
  private static final String SCREENING_RECOMMENDATIONS_CODE = "screening-recommendations";

  private final FhirSystems fhirSystems;
  private final boolean useUpsertInsteadOfConditionalUpdate;
  private final int chunkSize;

  public EligibilityBundleBuilder(
      FhirSystems fhirSystems,
      @Value("${fhir.use-upsert-instead-of-conditional-update}")
          boolean useUpsertInsteadOfConditionalUpdate,
      @Value("${query-sql-on-fhir.transaction-bundle-chunk-size}") int chunkSize) {
    this.fhirSystems = fhirSystems;
    this.useUpsertInsteadOfConditionalUpdate = useUpsertInsteadOfConditionalUpdate;
    this.chunkSize = chunkSize;
  }

  /**
   * One transaction bundle per chunk of at most {@code chunkSize} patients, each entry containing
   * that patient's ResearchSubject plus one Observation per criterion. Empty if {@code results} is
   * empty.
   */
  public List<Bundle> buildSubjectAndObservationBundles(
      ResearchStudy study, List<PatientEligibilityResult> results, Date effectiveDate) {
    var studyId = study.getIdElement().getIdPart();
    var researchStudyReference = new Reference("ResearchStudy/" + studyId);

    var bundles = new ArrayList<Bundle>();
    for (var chunk : partition(results, chunkSize)) {
      var bundle = new Bundle().setType(Bundle.BundleType.TRANSACTION);
      bundle.setTimestamp(effectiveDate);

      for (var result : chunk) {
        addResearchSubjectEntry(bundle, studyId, researchStudyReference, result.patientId());

        for (var outcome : result.criteria()) {
          addObservationEntry(
              bundle, studyId, researchStudyReference, result.patientId(), outcome, effectiveDate);
        }
      }

      bundles.add(bundle);
    }

    return bundles;
  }

  private void addResearchSubjectEntry(
      Bundle bundle, String studyId, Reference researchStudyReference, String patientId) {
    var subject =
        new ResearchSubject()
            .setStudy(researchStudyReference)
            .setIndividual(new Reference("Patient/" + patientId))
            .setStatus(ResearchSubject.ResearchSubjectStatus.CANDIDATE);

    var request = new BundleEntryRequestComponent();
    if (useUpsertInsteadOfConditionalUpdate) {
      var idValue =
          "ResearchSubject?patient=Patient/" + patientId + "&study=ResearchStudy/" + studyId;
      var resourceId = Hashing.sha256().hashString(idValue, StandardCharsets.UTF_8).toString();
      subject.setId(resourceId);
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(ResourceType.ResearchSubject.name() + "/" + resourceId);
    } else {
      request
          .setMethod(Bundle.HTTPVerb.POST)
          .setIfNoneExist(
              "ResearchSubject?patient=Patient/" + patientId + "&study=ResearchStudy/" + studyId)
          .setUrl(ResourceType.ResearchSubject.name());
    }

    bundle
        .addEntry()
        .setResource(subject)
        .setFullUrl(IdType.newRandomUuid().getValue())
        .setRequest(request);
  }

  private void addObservationEntry(
      Bundle bundle,
      String studyId,
      Reference researchStudyReference,
      String patientId,
      CriterionOutcome outcome,
      Date effectiveDate) {
    var libraryId = outcome.library().getIdElement().getIdPart();

    var observation = new Observation();
    observation.setStatus(Observation.ObservationStatus.FINAL);
    observation
        .addCategory()
        .addCoding(
            new Coding()
                .setSystem(fhirSystems.eligibilityObservationCategorySystem())
                .setCode(ELIGIBILITY_ASSESSMENT_CODE));
    observation.setCode(new CodeableConcept().setText(outcome.displayText()));
    observation.setSubject(new Reference("Patient/" + patientId));
    observation.addFocus(researchStudyReference);
    observation.addDerivedFrom(new Reference("Library/" + libraryId));
    observation.setEffective(new DateTimeType(effectiveDate));

    if (outcome.met() == null) {
      observation.setDataAbsentReason(
          new CodeableConcept()
              .addCoding(
                  new Coding()
                      .setSystem(DATA_ABSENT_REASON_SYSTEM)
                      .setCode(DATA_ABSENT_REASON_UNKNOWN)));
    } else {
      observation.setValue(new BooleanType(outcome.met()));
    }

    var conditionalUrl =
        "Observation?subject=Patient/"
            + patientId
            + "&focus=ResearchStudy/"
            + studyId
            + "&derived-from=Library/"
            + libraryId;

    var request = new BundleEntryRequestComponent();
    if (useUpsertInsteadOfConditionalUpdate) {
      var resourceId =
          Hashing.sha256().hashString(conditionalUrl, StandardCharsets.UTF_8).toString();
      observation.setId(resourceId);
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(ResourceType.Observation.name() + "/" + resourceId);
    } else {
      request.setMethod(Bundle.HTTPVerb.PUT).setUrl(conditionalUrl);
    }

    bundle
        .addEntry()
        .setResource(observation)
        .setFullUrl(IdType.newRandomUuid().getValue())
        .setRequest(request);
  }

  /** A single transaction bundle updating the study's screening List to the given membership. */
  public Bundle buildScreeningListBundle(
      ResearchStudy study,
      List<PatientEligibilityResult> results,
      Optional<ListResource> previousList) {
    var studyId = study.getIdElement().getIdPart();

    var bundle = new Bundle().setType(Bundle.BundleType.TRANSACTION);
    bundle.setTimestamp(new Date());

    var screeningListCode = new CodeableConcept();
    screeningListCode
        .addCoding()
        .setSystem(fhirSystems.screeningListCodeSystem())
        .setCode(SCREENING_RECOMMENDATIONS_CODE);

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
        .setValue(new Reference("ResearchStudy/" + studyId));

    for (var result : results) {
      var patientId = result.patientId();
      var individualReferenceValue = "Patient/" + patientId;
      var studyReferenceValue = "ResearchStudy/" + studyId;

      // Conditional reference, not a bundle-internal fullUrl - see the class Javadoc.
      var itemReference =
          new Reference()
              .setReference(
                  "ResearchSubject?patient="
                      + individualReferenceValue
                      + "&study="
                      + studyReferenceValue);

      var listEntry =
          new ListResource.ListEntryComponent().setItem(itemReference).setDate(new Date());

      if (previousList.isPresent()) {
        var previousEntry =
            previousList.get().getEntry().stream()
                .filter(
                    item ->
                        ((ResearchSubject) item.getItem().getResource())
                                .getIndividual()
                                .getReference()
                                .equals(individualReferenceValue)
                            && ((ResearchSubject) item.getItem().getResource())
                                .getStudy()
                                .getReference()
                                .equals(studyReferenceValue))
                .findFirst();

        if (previousEntry.isPresent() && previousEntry.get().hasDate()) {
          listEntry.setDate(previousEntry.get().getDate());
        }
      }

      screeningList.addEntry(listEntry);
    }

    var request = new BundleEntryRequestComponent();
    if (useUpsertInsteadOfConditionalUpdate) {
      var identifierValue =
          fhirSystems.screeningListIdentifier() + "|" + study.getIdentifierFirstRep().getValue();
      var resourceId =
          Hashing.sha256().hashString(identifierValue, StandardCharsets.UTF_8).toString();
      screeningList.setId(resourceId);
      request.setMethod(Bundle.HTTPVerb.PUT).setUrl(ResourceType.List.name() + "/" + resourceId);
    } else {
      request
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
        .setRequest(request);

    return bundle;
  }

  private static <T> List<List<T>> partition(List<T> items, int size) {
    if (items.isEmpty()) {
      return List.of();
    }

    var chunks = new ArrayList<List<T>>();
    for (var i = 0; i < items.size(); i += size) {
      chunks.add(items.subList(i, Math.min(i + size, items.size())));
    }
    return chunks;
  }
}
