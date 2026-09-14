package org.miracum.recruit.querysqlonfhir;

import static org.assertj.core.api.Assertions.assertThat;

import com.google.common.hash.Hashing;
import io.github.miracum.recruit.Recruit;
import java.nio.charset.StandardCharsets;
import java.util.Date;
import java.util.List;
import java.util.Set;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.hl7.fhir.r4.model.ResearchSubject;
import org.junit.jupiter.api.Test;

class EligibilityBundleBuilderTest {

  private static final String RESEARCH_SUBJECT_IDENTIFIER_SYSTEM =
      Recruit.NamingSystems.ResearchSubjectId.uri();

  private static ResearchStudy studyWithId(String id) {
    var study = new ResearchStudy();
    study.setId(id);
    return study;
  }

  private static String researchSubjectId(String patientId, String studyId) {
    return Hashing.sha256()
        .hashString(
            "ResearchSubject?patient=Patient/" + patientId + "&study=ResearchStudy/" + studyId,
            StandardCharsets.UTF_8)
        .toString();
  }

  /**
   * Without update-as-create, ResearchSubject is created via conditional POST regardless of {@code
   * existingResearchSubjectIds} - the server's If-None-Exist handling is what keeps existing
   * subjects untouched, not this class, so the set is irrelevant here.
   */
  @Test
  void buildSubjectAndObservationBundle_withoutUpdateAsCreate_usesConditionalPost() {
    var sut = new EligibilityBundleBuilder(false, 500);
    var study = studyWithId("study-1");
    var batch = List.of(new PatientEligibilityResult("patient-1", List.of()));

    var bundle = sut.buildSubjectAndObservationBundle(study, batch, new Date(), Set.of());

    var entry = bundle.getEntry().getFirst();
    assertThat(entry.getRequest().getMethod()).isEqualTo(Bundle.HTTPVerb.POST);
    assertThat(entry.getRequest().getIfNoneExist())
        .isEqualTo("ResearchSubject?patient=Patient/patient-1&study=ResearchStudy/study-1");
    assertThat(((ResearchSubject) entry.getResource()).getStatus())
        .isEqualTo(ResearchSubject.ResearchSubjectStatus.CANDIDATE);
  }

  @Test
  void buildSubjectAndObservationBundle_withUpdateAsCreate_createsNewSubjectViaPut() {
    var sut = new EligibilityBundleBuilder(true, 500);
    var study = studyWithId("study-1");
    var batch = List.of(new PatientEligibilityResult("patient-1", List.of()));

    var bundle = sut.buildSubjectAndObservationBundle(study, batch, new Date(), Set.of());

    var entry = bundle.getEntry().getFirst();
    var expectedId = researchSubjectId("patient-1", "study-1");
    assertThat(entry.getRequest().getMethod()).isEqualTo(Bundle.HTTPVerb.PUT);
    assertThat(entry.getRequest().getUrl()).isEqualTo("ResearchSubject/" + expectedId);
    assertThat(((ResearchSubject) entry.getResource()).getStatus())
        .isEqualTo(ResearchSubject.ResearchSubjectStatus.CANDIDATE);
  }

  /**
   * The clobbering scenario this guards against: a subject that already exists (its status may
   * since have been changed by list-next) must not be rewritten by a later poll cycle, or its
   * status would be silently reset to CANDIDATE.
   */
  @Test
  void buildSubjectAndObservationBundle_withUpdateAsCreate_skipsExistingSubject() {
    var sut = new EligibilityBundleBuilder(true, 500);
    var study = studyWithId("study-1");
    var batch = List.of(new PatientEligibilityResult("patient-1", List.of()));
    var existingId = researchSubjectId("patient-1", "study-1");

    var bundle = sut.buildSubjectAndObservationBundle(study, batch, new Date(), Set.of(existingId));

    assertThat(bundle.getEntry()).isEmpty();
  }

  /** A mixed batch: the new patient still gets created, the existing one is left alone. */
  @Test
  void buildSubjectAndObservationBundle_withUpdateAsCreate_onlySkipsKnownExistingSubjects() {
    var sut = new EligibilityBundleBuilder(true, 500);
    var study = studyWithId("study-1");
    var batch =
        List.of(
            new PatientEligibilityResult("existing-patient", List.of()),
            new PatientEligibilityResult("new-patient", List.of()));
    var existingId = researchSubjectId("existing-patient", "study-1");

    var bundle = sut.buildSubjectAndObservationBundle(study, batch, new Date(), Set.of(existingId));

    var entries = bundle.getEntry();
    assertThat(entries).hasSize(1);
    assertThat(entries.getFirst().getRequest().getUrl())
        .isEqualTo("ResearchSubject/" + researchSubjectId("new-patient", "study-1"));
  }

  /**
   * list-next keys its screening-notes table on this identifier rather than the FHIR logical id, so
   * it must be set unconditionally - independent of whether the subject is addressed via
   * conditional POST or update-as-create PUT.
   */
  @Test
  void buildSubjectAndObservationBundle_withoutUpdateAsCreate_setsResearchSubjectIdentifier() {
    var sut = new EligibilityBundleBuilder(false, 500);
    var study = studyWithId("study-1");
    var batch = List.of(new PatientEligibilityResult("patient-1", List.of()));

    var bundle = sut.buildSubjectAndObservationBundle(study, batch, new Date(), Set.of());

    var subject = (ResearchSubject) bundle.getEntry().getFirst().getResource();
    assertThat(subject.getIdentifier()).hasSize(1);
    var identifier = subject.getIdentifierFirstRep();
    assertThat(identifier.getSystem()).isEqualTo(RESEARCH_SUBJECT_IDENTIFIER_SYSTEM);
    assertThat(identifier.getValue()).isEqualTo(researchSubjectId("patient-1", "study-1"));
  }

  @Test
  void buildSubjectAndObservationBundle_withUpdateAsCreate_setsResearchSubjectIdentifier() {
    var sut = new EligibilityBundleBuilder(true, 500);
    var study = studyWithId("study-1");
    var batch = List.of(new PatientEligibilityResult("patient-1", List.of()));

    var bundle = sut.buildSubjectAndObservationBundle(study, batch, new Date(), Set.of());

    var subject = (ResearchSubject) bundle.getEntry().getFirst().getResource();
    var identifier = subject.getIdentifierFirstRep();
    assertThat(identifier.getSystem()).isEqualTo(RESEARCH_SUBJECT_IDENTIFIER_SYSTEM);
    // Same deterministic hash used as the resource's own id in update-as-create mode - see
    // EligibilityBundleBuilder.researchSubjectResourceId.
    assertThat(identifier.getValue()).isEqualTo(subject.getIdElement().getIdPart());
  }

  @Test
  void chunkSize_returnsConfiguredValue() {
    var sut = new EligibilityBundleBuilder(false, 500);

    assertThat(sut.chunkSize()).isEqualTo(500);
  }
}
