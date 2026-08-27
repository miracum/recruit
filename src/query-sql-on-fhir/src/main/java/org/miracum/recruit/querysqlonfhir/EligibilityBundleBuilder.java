package org.miracum.recruit.querysqlonfhir;

import com.google.common.hash.Hashing;
import de.medizininformatikinitiative.kerndatensatz.studie.Studie;
import io.github.dizuker.tofhir.IdUtils;
import io.github.miracum.recruit.Recruit;
import java.nio.charset.StandardCharsets;
import java.util.Date;
import java.util.List;
import java.util.Optional;
import java.util.Set;
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
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;
import org.springframework.util.StringUtils;

/**
 * Builds the FHIR transaction bundles {@link PollForStudies} submits from a study's merged
 * eligibility results: one bundle of ResearchSubject + per-criterion Observation entries per {@link
 * #chunkSize}-sized batch of patients (see {@link #buildSubjectAndObservationBundle} - {@link
 * PollForStudies} calls it once per batch as it streams results in, rather than this class chunking
 * a full, already-materialized result set itself), and a separate screening List bundle (see {@link
 * #buildScreeningListBundle}) that must be submitted afterwards.
 *
 * <p>The List references each patient's ResearchSubject via a <a
 * href="https://www.hl7.org/fhir/http.html#trules">conditional reference</a> (a search URL, not a
 * bundle-internal {@code fullUrl}) precisely so it can be built and submitted independently of -
 * and after - whichever chunk actually created or updated that ResearchSubject; the server resolves
 * it against whatever now matches on the server, without this class needing to track
 * server-assigned ids across chunk submissions. When {@code useUpdateAsCreate} is enabled, the
 * ResearchSubject's id is instead deterministic (a hash of the same patient/study pair - see {@link
 * #researchSubjectResourceId}), so a direct reference to that id is used in place of the
 * conditional one, for FHIR servers (e.g. Blaze) that don't support conditional references here.
 *
 * <p>Either way, a ResearchSubject that already exists is never rewritten by this class: once
 * created, its {@code status} is owned by list-next (see {@code ResearchSubjectService.cs}), which
 * updates it via a version-aware PUT as users triage candidates. Without {@code useUpdateAsCreate},
 * this falls out of the FHIR server's conditional-create semantics for free (a {@code POST} with
 * {@code If-None-Exist} is a no-op if a match already exists). With {@code useUpdateAsCreate}, a
 * plain PUT-by-id would instead unconditionally replace the whole resource - resetting {@code
 * status} back to {@code candidate} and dropping any notes - every poll cycle, so {@link
 * #buildSubjectAndObservationBundle} is told which ids already exist and skips them explicitly.
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

  private final boolean useUpdateAsCreate;
  private final int chunkSize;

  public EligibilityBundleBuilder(
      @Value("${fhir.use-update-as-create}") boolean useUpdateAsCreate,
      @Value("${query-sql-on-fhir.transaction-bundle-chunk-size}") int chunkSize) {
    this.useUpdateAsCreate = useUpdateAsCreate;
    this.chunkSize = chunkSize;
  }

  /**
   * Whether ResearchSubjects are written via update-as-create (PUT to a deterministic id) rather
   * than conditional create (POST with If-None-Exist). Callers use this to decide whether they need
   * to look up already-existing ResearchSubject ids before calling {@link
   * #buildSubjectAndObservationBundle} - see that method's {@code existingResearchSubjectIds}.
   */
  public boolean usesUpdateAsCreate() {
    return useUpdateAsCreate;
  }

  /**
   * The max number of patients' worth of entries {@link #buildSubjectAndObservationBundle} will
   * accept in one call - callers streaming results in (see {@code PollForStudies}) should buffer up
   * to this many {@link PatientEligibilityResult}s before building and submitting a bundle, to stay
   * under the FHIR server's transaction limits.
   */
  public int chunkSize() {
    return chunkSize;
  }

  /**
   * One transaction bundle for the given batch of patients (expected to be at most {@link
   * #chunkSize} patients - see there), containing each patient's ResearchSubject (unless it already
   * exists - see {@code existingResearchSubjectIds}) plus one Observation per criterion.
   *
   * @param existingResearchSubjectIds ids (as returned by {@link #researchSubjectResourceId}) of
   *     ResearchSubjects already on the server for this study. Only consulted when {@code
   *     useUpdateAsCreate} is enabled - without it, {@code POST}'s conditional-create already
   *     guarantees existing subjects are left untouched, so callers may pass {@link Set#of()}.
   */
  public Bundle buildSubjectAndObservationBundle(
      ResearchStudy study,
      List<PatientEligibilityResult> batch,
      Date effectiveDate,
      Set<String> existingResearchSubjectIds) {
    var studyId = study.getIdElement().getIdPart();
    var researchStudyReference = new Reference("ResearchStudy/" + studyId);

    var bundle = new Bundle().setType(Bundle.BundleType.TRANSACTION);
    bundle.setTimestamp(effectiveDate);

    for (var result : batch) {
      addResearchSubjectEntry(
          bundle, studyId, researchStudyReference, result.patientId(), existingResearchSubjectIds);

      for (var outcome : result.criteria()) {
        addObservationEntry(
            bundle, studyId, researchStudyReference, result.patientId(), outcome, effectiveDate);
      }
    }

    return bundle;
  }

  private void addResearchSubjectEntry(
      Bundle bundle,
      String studyId,
      Reference researchStudyReference,
      String patientId,
      Set<String> existingResearchSubjectIds) {
    var request = new BundleEntryRequestComponent();
    var subject =
        new ResearchSubject()
            .setStudy(researchStudyReference)
            .setIndividual(new Reference("Patient/" + patientId))
            .setStatus(ResearchSubject.ResearchSubjectStatus.CANDIDATE);

    if (useUpdateAsCreate) {
      var resourceId = researchSubjectResourceId(patientId, studyId);
      if (existingResearchSubjectIds.contains(resourceId)) {
        // Already created by a previous poll cycle. A PUT is a full replace, and this
        // freshly
        // built subject only knows CANDIDATE - PUTting it again would reset a status
        // list-next
        // has since moved on, and drop any notes appended there. Leave it alone; its
        // Observations
        // are still refreshed below regardless.
        return;
      }
      subject.setId(resourceId);
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(ResourceType.ResearchSubject.name() + "/" + resourceId);
    } else {
      request
          .setMethod(Bundle.HTTPVerb.POST)
          .setIfNoneExist(researchSubjectConditionalReferenceValue(patientId, studyId))
          .setUrl(ResourceType.ResearchSubject.name());
    }

    // Set unconditionally - independent of useUpdateAsCreate above, which only
    // controls how the
    // subject is addressed on the wire (a direct id vs. a conditional reference).
    // This identifier
    // is what list-next keys its screening-notes table on, so it must be stable and
    // present
    // either way.
    subject
        .addIdentifier()
        .setSystem(Recruit.NamingSystems.ResearchSubjectId.uri())
        .setValue(researchSubjectResourceId(patientId, studyId));

    bundle
        .addEntry()
        .setResource(subject)
        .setFullUrl(IdType.newRandomUuid().getValue())
        .setRequest(request);
  }

  private static String researchSubjectConditionalReferenceValue(String patientId, String studyId) {
    return "ResearchSubject?patient=Patient/" + patientId + "&study=ResearchStudy/" + studyId;
  }

  /**
   * The deterministic id a ResearchSubject is created/updated under when {@code useUpdateAsCreate}
   * is enabled - a hash of the same patient/study pair that would otherwise identify it via a
   * conditional reference, so the two addressing schemes stay in sync.
   */
  private static String researchSubjectResourceId(String patientId, String studyId) {
    return Hashing.sha256()
        .hashString(
            researchSubjectConditionalReferenceValue(patientId, studyId), StandardCharsets.UTF_8)
        .toString();
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
    observation.addCategory(
        new CodeableConcept()
            .addCoding(
                Recruit.CodeSystems.EligibilityAssessmentCategory.ELIGIBILITY_ASSESSMENT.coding()));
    observation.setCode(new CodeableConcept().setText(outcome.displayText()));
    observation.setSubject(new Reference("Patient/" + patientId));
    observation.addFocus(researchStudyReference);
    // Library isn't a valid Observation.derivedFrom target, so the criterion is
    // referenced via a
    // custom extension instead of a core element.
    observation.addExtension(
        Recruit.Extensions.eligibilityAssessmentDerivedFromLibrary(
            new Reference("Library/" + libraryId)));
    observation.setEffective(new DateTimeType(effectiveDate));
    observation.setValue(buildResultValue(outcome));

    if (outcome.note() != null && !outcome.note().isBlank()) {
      observation.addNote().setText(outcome.note()).setTime(new Date());
    }

    // The Library extension above isn't searchable without a custom
    // SearchParameter, so identity
    // for this Observation - and thus the conditional-update key - is expressed as
    // a business
    // identifier instead, keyed on the standard, always-searchable `identifier`
    // parameter.
    var identifierValue =
        Hashing.sha256()
            .hashString(
                "patient=" + patientId + ";study=" + studyId + ";library=" + libraryId,
                StandardCharsets.UTF_8)
            .toString();
    observation
        .addIdentifier()
        .setSystem(Recruit.NamingSystems.EligibilityAssessmentId.uri())
        .setValue(identifierValue);

    var request = new BundleEntryRequestComponent();
    if (useUpdateAsCreate) {
      observation.setId(identifierValue);
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(ResourceType.Observation.name() + "/" + identifierValue);
    } else {
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(
              "Observation?identifier="
                  + Recruit.NamingSystems.EligibilityAssessmentId.uri()
                  + "|"
                  + identifierValue);
    }

    bundle
        .addEntry()
        .setResource(observation)
        .setFullUrl(IdType.newRandomUuid().getValue())
        .setRequest(request);
  }

  /**
   * A single transaction bundle updating the study's screening List to the given membership.
   *
   * @param patientIds every candidate/undecidable patient's id - just the id, not the full {@link
   *     PatientEligibilityResult}, since the List only ever references a patient's ResearchSubject
   *     and doesn't otherwise need their per-criterion outcomes.
   */
  public Bundle buildScreeningListBundle(
      ResearchStudy study, List<String> patientIds, Optional<ListResource> previousList) {
    var studyId = study.getIdElement().getIdPart();

    var bundle = new Bundle().setType(Bundle.BundleType.TRANSACTION);
    bundle.setTimestamp(new Date());

    var screeningListCode = new CodeableConcept();
    screeningListCode
        .addCoding()
        .setSystem(Recruit.CodeSystems.screeningListType())
        .setCode(
            Recruit.CodeSystems.ScreeningListType.SCREENING_RECOMMENDATIONS.coding().getCode());

    var screeningList =
        new ListResource()
            .setStatus(ListStatus.CURRENT)
            .setMode(ListResource.ListMode.WORKING)
            .setCode(screeningListCode);
    var screeningListIdentifier =
        screeningList
            .addIdentifier()
            .setSystem(Recruit.NamingSystems.ScreeningListId.uri())
            .setValue(study.getIdentifierFirstRep().getValue());

    var studyReference =
        new Reference("ResearchStudy/" + studyId).setDisplay(getStudyAcronym(study));
    screeningList.addExtension(Recruit.Extensions.belongsToStudy(studyReference));

    for (var patientId : patientIds) {
      var individualReferenceValue = "Patient/" + patientId;
      var studyReferenceValue = "ResearchStudy/" + studyId;

      Reference itemReference;
      if (useUpdateAsCreate) {
        itemReference =
            new Reference("ResearchSubject/" + researchSubjectResourceId(patientId, studyId));
      } else {
        itemReference =
            new Reference()
                .setReference(
                    "ResearchSubject?patient="
                        + individualReferenceValue
                        + "&study="
                        + studyReferenceValue);
      }

      var listEntry =
          new ListResource.ListEntryComponent().setItem(itemReference).setDate(new Date());

      if (previousList.isPresent()) {
        // itemReference is built the same way for a given patient/study pair on every
        // run, so
        // the previous entry for this patient can be matched by comparing that literal
        // reference string directly - no need to resolve item.getItem().getResource(),
        // which
        // isn't reliably populated (e.g. Blaze doesn't resolve it via the `_include`
        // used to
        // fetch previousList).
        var referenceValue = itemReference.getReference();
        var previousEntry =
            previousList.get().getEntry().stream()
                .filter(item -> referenceValue.equals(item.getItem().getReference()))
                .findFirst();

        if (previousEntry.isPresent() && previousEntry.get().hasDate()) {
          listEntry.setDate(previousEntry.get().getDate());
        }
      }

      screeningList.addEntry(listEntry);
    }

    var request = new BundleEntryRequestComponent();
    if (useUpdateAsCreate) {
      var id = IdUtils.fromIdentifier(screeningListIdentifier);
      screeningList.setId(id);
      request.setMethod(Bundle.HTTPVerb.PUT).setUrl(ResourceType.List.name() + "/" + id);
    } else {
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(
              "List?identifier="
                  + Recruit.NamingSystems.ScreeningListId.uri()
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

    return value;
  }

  /** Package-visible so {@link FunnelReportBuilder} can reuse it for the funnel's Measure title. */
  static String getStudyAcronym(ResearchStudy study) {
    var acronym = Studie.Extensions.getMiiExStudieAkronym(study);
    if (acronym != null && StringUtils.hasText(acronym.getValue())) {
      return acronym.getValue();
    }

    if (study.hasTitle()) {
      return study.getTitle();
    }
    return study.getIdElement().getIdPart();
  }
}
