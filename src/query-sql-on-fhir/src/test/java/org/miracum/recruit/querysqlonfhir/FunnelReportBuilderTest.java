package org.miracum.recruit.querysqlonfhir;

import static org.assertj.core.api.Assertions.assertThat;

import io.github.miracum.recruit.Recruit;
import java.util.Date;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Measure;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.junit.jupiter.api.Test;

class FunnelReportBuilderTest {

  private static ResearchStudy studyWithIdentifier(String id, String identifierValue) {
    var study = new ResearchStudy();
    study.setId(id);
    study.addIdentifier().setValue(identifierValue);
    return study;
  }

  private static Library libraryWithId(String id) {
    var library = new Library();
    library.setId(id);
    library.getContent().add(new org.hl7.fhir.r4.model.Attachment());
    return library;
  }

  private static FunnelResult funnelWith(long total, EligibilityCriterion... criteria) {
    var steps = new java.util.ArrayList<FunnelResult.Step>();
    long remaining = total;
    for (var criterion : criteria) {
      remaining -= 100;
      steps.add(new FunnelResult.Step(criterion, remaining));
    }
    return new FunnelResult(total, steps);
  }

  @Test
  void buildFunnelReportBundle_withoutUpdateAsCreate_usesConditionalPutByIdentifier() {
    var sut = new FunnelReportBuilder(false);
    var study = studyWithIdentifier("study-1", "trial-042");
    var criterion = new EligibilityCriterion(libraryWithId("lib-age"), "Age >= 18 years", false);
    var funnel = funnelWith(1000, criterion);

    var bundle = sut.buildFunnelReportBundle(study, funnel, new Date());

    assertThat(bundle.getEntry()).hasSize(2);
    var measureEntry = bundle.getEntry().get(0);
    var reportEntry = bundle.getEntry().get(1);

    assertThat(measureEntry.getRequest().getMethod()).isEqualTo(Bundle.HTTPVerb.PUT);
    assertThat(measureEntry.getRequest().getUrl())
        .isEqualTo(
            "Measure?identifier="
                + Recruit.NamingSystems.EligibilityFunnelMeasureId.uri()
                + "|trial-042");

    assertThat(reportEntry.getRequest().getMethod()).isEqualTo(Bundle.HTTPVerb.PUT);
    assertThat(reportEntry.getRequest().getUrl())
        .isEqualTo(
            "MeasureReport?identifier="
                + Recruit.NamingSystems.EligibilityFunnelReportId.uri()
                + "|trial-042");
  }

  @Test
  void buildFunnelReportBundle_withUpdateAsCreate_usesDeterministicIds() {
    var sut = new FunnelReportBuilder(true);
    var study = studyWithIdentifier("study-1", "trial-042");
    var criterion = new EligibilityCriterion(libraryWithId("lib-age"), "Age >= 18 years", false);
    var funnel = funnelWith(1000, criterion);

    var bundle = sut.buildFunnelReportBundle(study, funnel, new Date());

    var measureEntry = bundle.getEntry().get(0);
    var reportEntry = bundle.getEntry().get(1);

    assertThat(measureEntry.getRequest().getMethod()).isEqualTo(Bundle.HTTPVerb.PUT);
    assertThat(measureEntry.getRequest().getUrl())
        .isEqualTo("Measure/" + measureEntry.getResource().getIdElement().getIdPart());
    assertThat(reportEntry.getRequest().getMethod()).isEqualTo(Bundle.HTTPVerb.PUT);
    assertThat(reportEntry.getRequest().getUrl())
        .isEqualTo("MeasureReport/" + reportEntry.getResource().getIdElement().getIdPart());

    // Same study + same kind of resource always hashes to the same id, run to run.
    var bundle2 = sut.buildFunnelReportBundle(study, funnel, new Date());
    assertThat(bundle2.getEntry().get(0).getResource().getIdElement().getIdPart())
        .isEqualTo(measureEntry.getResource().getIdElement().getIdPart());
  }

  @Test
  void buildFunnelReportBundle_measureReportPointsAtTheMeasure() {
    var sut = new FunnelReportBuilder(false);
    var study = studyWithIdentifier("study-1", "trial-042");
    var criterion = new EligibilityCriterion(libraryWithId("lib-age"), "Age >= 18 years", false);
    var funnel = funnelWith(1000, criterion);

    var bundle = sut.buildFunnelReportBundle(study, funnel, new Date());

    var measure = (Measure) bundle.getEntry().get(0).getResource();
    var report = (MeasureReport) bundle.getEntry().get(1).getResource();

    assertThat(report.getStatus()).isEqualTo(MeasureReport.MeasureReportStatus.COMPLETE);
    assertThat(report.getType()).isEqualTo(MeasureReport.MeasureReportType.SUMMARY);
    assertThat(measure.getUrl()).isNotBlank();
    assertThat(report.getMeasure()).isEqualTo(measure.getUrl());
    assertThat(Recruit.Extensions.getBelongsToStudy(report).getReference())
        .isEqualTo("ResearchStudy/study-1");
  }

  /**
   * Regression test: {@code MeasureReport.measure} used to be built from {@code
   * measure.getIdElement().getIdPart()}, which is only ever populated in update-as-create mode (see
   * {@code FunnelReportBuilder.addPutEntry}) - without it, the Measure has no id yet at
   * bundle-build time, so this silently produced {@code "Measure/null"} in the (default)
   * conditional-PUT-by-identifier mode.
   */
  @Test
  void buildFunnelReportBundle_withoutUpdateAsCreate_measureReferenceIsNotNull() {
    var sut = new FunnelReportBuilder(false);
    var study = studyWithIdentifier("study-1", "trial-042");
    var funnel = funnelWith(1000);

    var bundle = sut.buildFunnelReportBundle(study, funnel, new Date());
    var report = (MeasureReport) bundle.getEntry().get(1).getResource();

    assertThat(report.getMeasure()).doesNotContain("null");
  }

  /**
   * The canonical url is what makes {@code MeasureReport.measure} resolvable regardless of
   * addressing mode (see the regression test above) - it must therefore not itself depend on which
   * mode is in use.
   */
  @Test
  void buildFunnelReportBundle_measureCanonicalUrlIsIndependentOfUpdateAsCreateMode() {
    var study = studyWithIdentifier("study-1", "trial-042");
    var funnel = funnelWith(1000);

    var withoutUpdateAsCreate =
        new FunnelReportBuilder(false).buildFunnelReportBundle(study, funnel, new Date());
    var withUpdateAsCreate =
        new FunnelReportBuilder(true).buildFunnelReportBundle(study, funnel, new Date());

    var measureWithout = (Measure) withoutUpdateAsCreate.getEntry().get(0).getResource();
    var measureWith = (Measure) withUpdateAsCreate.getEntry().get(0).getResource();

    assertThat(measureWithout.getUrl()).isEqualTo(measureWith.getUrl());
  }

  @Test
  void buildFunnelReportBundle_groupPopulationHasOneTotalStepPlusOnePerCriterion() {
    var sut = new FunnelReportBuilder(false);
    var study = studyWithIdentifier("study-1", "trial-042");
    var age = new EligibilityCriterion(libraryWithId("lib-age"), "Age >= 18 years", false);
    var chemo = new EligibilityCriterion(libraryWithId("lib-chemo"), "No prior chemo", true);
    var funnel = funnelWith(1000, age, chemo);

    var bundle = sut.buildFunnelReportBundle(study, funnel, new Date());
    var report = (MeasureReport) bundle.getEntry().get(1).getResource();

    var population = report.getGroupFirstRep().getPopulation();
    assertThat(population).hasSize(3);

    var total = population.get(0);
    assertThat(total.getCount()).isEqualTo(1000);
    assertThat(total.getCode().getCodingFirstRep().getCode()).isEqualTo("total-population");
    assertThat(
            total.getExtensionByUrl(
                Recruit.Extensions.Urls.eligibilityAssessmentDerivedFromLibrary()))
        .isNull();

    var afterAge = population.get(1);
    assertThat(afterAge.getCount()).isEqualTo(900);
    assertThat(afterAge.getCode().getCodingFirstRep().getCode()).isEqualTo("after-criterion");
    assertThat(afterAge.getCode().getText()).isEqualTo("Age >= 18 years");
    assertThat(
            afterAge.getExtensionByUrl(
                Recruit.Extensions.Urls.eligibilityAssessmentDerivedFromLibrary()))
        .isNotNull();

    var afterChemo = population.get(2);
    assertThat(afterChemo.getCount()).isEqualTo(800);
    assertThat(afterChemo.getCode().getText()).isEqualTo("No prior chemo");
  }

  @Test
  void buildFunnelReportBundle_measureTitleIncludesStudyAcronym() {
    var sut = new FunnelReportBuilder(false);
    var study = studyWithIdentifier("study-1", "trial-042");
    study.setTitle("A Study of Something");
    var funnel = funnelWith(1000);

    var bundle = sut.buildFunnelReportBundle(study, funnel, new Date());
    var measure = (Measure) bundle.getEntry().get(0).getResource();

    assertThat(measure.getTitle()).isEqualTo("A Study of Something eligibility attrition funnel");
    assertThat(measure.getStatus())
        .isEqualTo(org.hl7.fhir.r4.model.Enumerations.PublicationStatus.ACTIVE);
  }
}
