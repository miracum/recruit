package org.miracum.recruit.querysqlonfhir;

import com.google.common.hash.Hashing;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Optional;
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
import org.hl7.fhir.r4.model.StringType;
import org.miracum.recruit.querysqlonfhir.config.FhirSystems;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;
import recruit.Recruit;

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

  private static final String SNOMED_SYSTEM = "http://snomed.info/sct";
  private static final String SNOMED_CODE_YES = "373066001";
  private static final String SNOMED_DISPLAY_YES = "Yes";
  private static final String SNOMED_CODE_NO = "373067005";
  private static final String SNOMED_DISPLAY_NO = "No";
  private static final String SNOMED_CODE_UNKNOWN = "261665006";
  private static final String SNOMED_DISPLAY_UNKNOWN = "Unknown";
  private static final String SNOMED_CODE_INDETERMINATE = "82334004";
  private static final String SNOMED_DISPLAY_INDETERMINATE = "Indeterminate";
  private static final String ELIGIBILITY_ASSESSMENT_CODE = "eligibility-assessment";

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
    // Library isn't a valid Observation.derivedFrom target, so the criterion is referenced via a
    // custom extension instead of a core element.
    observation
        .addExtension()
        .setUrl(fhirSystems.eligibilityObservationLibraryExtension())
        .setValue(new Reference("Library/" + libraryId));
    observation.setEffective(new DateTimeType(effectiveDate));
    observation.setValue(buildResultValue(outcome));

    // The Library extension above isn't searchable without a custom SearchParameter, so identity
    // for this Observation - and thus the conditional-update key - is expressed as a business
    // identifier instead, keyed on the standard, always-searchable `identifier` parameter.
    var identifierValue =
        Hashing.sha256()
            .hashString(
                "patient=" + patientId + ";study=" + studyId + ";library=" + libraryId,
                StandardCharsets.UTF_8)
            .toString();
    observation
        .addIdentifier()
        .setSystem(fhirSystems.eligibilityObservationIdentifierSystem())
        .setValue(identifierValue);

    var request = new BundleEntryRequestComponent();
    if (useUpsertInsteadOfConditionalUpdate) {
      observation.setId(identifierValue);
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(ResourceType.Observation.name() + "/" + identifierValue);
    } else {
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(
              "Observation?identifier="
                  + fhirSystems.eligibilityObservationIdentifierSystem()
                  + "|"
                  + identifierValue);
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
        .setSystem(Recruit.CodeSystems.ScreeningListType)
        .setCode(
            Recruit.CodeSystems.ScreeningListType.SCREENING_RECOMMENDATIONS.coding().getCode());

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
        .setValue(new Reference("ResearchStudy/" + studyId).setDisplay(getStudyAcronym(study)));

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

  /**
   * Maps a criterion outcome to a SNOMED CT "Yes/No/Unknown/Indeterminate (qualifier value)" coding
   * rather than {@code valueBoolean}/{@code dataAbsentReason}: {@code met=null} splits into two
   * genuinely different situations - {@code indeterminate=false} means the underlying data was
   * simply missing, {@code indeterminate=true} means the criterion's SQL was evaluated but reached
   * an inconclusive result - and only a real value (not an absent one qualified by a reason) can
   * carry that distinction. Both still count as "unresolved" for merge purposes (see
   * PatientEligibilityResult#overallMet) - this only changes how the result is displayed.
   *
   * <p>When the criterion's SQL provided a {@code result_note}, it's set as this CodeableConcept's
   * {@code text} - supplementing, not replacing, the coding's own {@code display} - so a criterion
   * author can explain e.g. *why* a result is indeterminate.
   */
  private CodeableConcept buildResultValue(CriterionOutcome outcome) {
    String code;
    String display;
    if (Boolean.TRUE.equals(outcome.met())) {
      code = SNOMED_CODE_YES;
      display = SNOMED_DISPLAY_YES;
    } else if (Boolean.FALSE.equals(outcome.met())) {
      code = SNOMED_CODE_NO;
      display = SNOMED_DISPLAY_NO;
    } else if (outcome.indeterminate()) {
      code = SNOMED_CODE_INDETERMINATE;
      display = SNOMED_DISPLAY_INDETERMINATE;
    } else {
      code = SNOMED_CODE_UNKNOWN;
      display = SNOMED_DISPLAY_UNKNOWN;
    }

    var value =
        new CodeableConcept()
            .addCoding(new Coding().setSystem(SNOMED_SYSTEM).setCode(code).setDisplay(display));
    if (outcome.note() != null && !outcome.note().isBlank()) {
      value.setText(outcome.note());
    }

    return value;
  }

  private String getStudyAcronym(ResearchStudy study) {
    var extension = study.getExtensionByUrl(fhirSystems.researchStudyAcronymExtension());
    if (extension != null && extension.getValue() instanceof StringType acronym) {
      return acronym.getValue();
    }

    return study.hasTitle() ? study.getTitle() : study.getIdElement().getIdPart();
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
