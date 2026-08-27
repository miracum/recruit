package org.miracum.recruit.querysqlonfhir;

import io.github.dizuker.tofhir.IdUtils;
import io.github.miracum.recruit.Recruit;
import java.util.Date;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Bundle.BundleEntryRequestComponent;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.DomainResource;
import org.hl7.fhir.r4.model.Enumerations;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.Identifier;
import org.hl7.fhir.r4.model.Measure;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Period;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.hl7.fhir.r4.model.ResourceType;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * Builds the small (always exactly 2 entries, regardless of population size) transaction bundle
 * that publishes a study's {@link FunnelResult} as FHIR: a {@code Measure} - created once and
 * otherwise left alone, existing purely so {@code MeasureReport.measure} (1..1 in the base R4 spec)
 * has something real to resolve, since none of this module's own logic runs as a quality-measure
 * evaluation - and a {@code MeasureReport} rebuilt every poll cycle, one {@code group.population}
 * entry per funnel step in {@code Group.characteristic} order (see {@link FunnelResult}).
 *
 * <p>Both resources are addressed the same way {@link EligibilityBundleBuilder}'s screening {@code
 * List} already is: PUT unconditionally every cycle (either update-as-create by a computed id, or a
 * conditional PUT by business identifier), never skipped-if-existing - unlike {@code
 * ResearchSubject}, neither resource has any list-next-owned state a later poll cycle could
 * clobber, so there's no reason to special-case an already-existing one.
 */
@Component
public class FunnelReportBuilder {

  private final boolean useUpdateAsCreate;

  public FunnelReportBuilder(@Value("${fhir.use-update-as-create}") boolean useUpdateAsCreate) {
    this.useUpdateAsCreate = useUpdateAsCreate;
  }

  /**
   * One transaction bundle publishing {@code funnel} for {@code study}: its {@code Measure} (see
   * {@link #buildMeasure}) plus a freshly rebuilt {@code MeasureReport} (see {@link
   * #buildMeasureReport}).
   */
  public Bundle buildFunnelReportBundle(
      ResearchStudy study, FunnelResult funnel, Date effectiveDate) {
    var identifierValue = study.getIdentifierFirstRep().getValue();

    var bundle = new Bundle().setType(Bundle.BundleType.TRANSACTION);
    bundle.setTimestamp(effectiveDate);

    var measureIdentifier =
        new Identifier()
            .setSystem(Recruit.NamingSystems.EligibilityFunnelMeasureId.uri())
            .setValue(identifierValue);
    // MeasureReport.measure (1..1 in the base R4 spec) needs a stable reference at the point this
    // bundle is built - before either resource has a server-assigned id, and independent of
    // whichever addressing scheme (see addPutEntry) the Measure itself ends up stored under. A
    // canonical url computed the same deterministic way every other id/reference in this module
    // is (see IdUtils.fromIdentifier) gives it exactly that, without a second round trip to look
    // the id up after submission.
    var measureCanonicalUrl =
        "https://miracum.github.io/recruit/fhir/Measure/"
            + IdUtils.fromIdentifier(measureIdentifier).getIdPart();

    var measure = buildMeasure(study, measureCanonicalUrl);
    measure.addIdentifier(measureIdentifier);
    addPutEntry(bundle, measure, ResourceType.Measure, measureIdentifier);

    var report = buildMeasureReport(study, funnel, effectiveDate, measureCanonicalUrl);
    var reportIdentifier =
        report
            .addIdentifier()
            .setSystem(Recruit.NamingSystems.EligibilityFunnelReportId.uri())
            .setValue(identifierValue);
    addPutEntry(bundle, report, ResourceType.MeasureReport, reportIdentifier);

    return bundle;
  }

  private Measure buildMeasure(ResearchStudy study, String canonicalUrl) {
    var measure = new Measure();
    measure.setUrl(canonicalUrl);
    measure.setStatus(Enumerations.PublicationStatus.ACTIVE);
    measure.setTitle(
        EligibilityBundleBuilder.getStudyAcronym(study) + " eligibility attrition funnel");
    return measure;
  }

  private MeasureReport buildMeasureReport(
      ResearchStudy study, FunnelResult funnel, Date effectiveDate, String measureUrl) {
    var report = new MeasureReport();
    report.setStatus(MeasureReport.MeasureReportStatus.COMPLETE);
    report.setType(MeasureReport.MeasureReportType.SUMMARY);
    report.setMeasure(measureUrl);
    report.setDate(effectiveDate);
    report.setPeriod(new Period().setStart(effectiveDate).setEnd(effectiveDate));
    report.addExtension(
        Recruit.Extensions.belongsToStudy(
            new Reference("ResearchStudy/" + study.getIdElement().getIdPart())));

    var group = report.addGroup();
    group.setCode(new CodeableConcept().setText("Eligibility attrition funnel"));

    var totalPopulation = group.addPopulation();
    totalPopulation.setCode(
        new CodeableConcept()
            .addCoding(
                Recruit.CodeSystems.EligibilityFunnelPopulationType.TOTAL_POPULATION.coding()));
    totalPopulation.setCount((int) funnel.totalPopulation());

    for (var step : funnel.steps()) {
      var population = group.addPopulation();
      population.setCode(
          new CodeableConcept()
              .addCoding(
                  Recruit.CodeSystems.EligibilityFunnelPopulationType.AFTER_CRITERION.coding())
              .setText(step.criterion().displayText()));
      population.setCount((int) step.remainingCount());
      population.addExtension(
          Recruit.Extensions.eligibilityAssessmentDerivedFromLibrary(
              new Reference("Library/" + step.criterion().library().getIdElement().getIdPart())));
    }

    return report;
  }

  /**
   * PUTs {@code resource} either by a deterministic update-as-create id or (equivalently, but
   * server-conditional) by its business identifier - the exact same two addressing schemes {@link
   * EligibilityBundleBuilder} already uses for every other resource this module writes.
   */
  private void addPutEntry(
      Bundle bundle, DomainResource resource, ResourceType resourceType, Identifier identifier) {
    var request = new BundleEntryRequestComponent();
    if (useUpdateAsCreate) {
      var id = IdUtils.fromIdentifier(identifier);
      resource.setId(id);
      request.setMethod(Bundle.HTTPVerb.PUT).setUrl(resourceType.name() + "/" + id);
    } else {
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(
              resourceType.name()
                  + "?identifier="
                  + identifier.getSystem()
                  + "|"
                  + identifier.getValue());
    }

    bundle
        .addEntry()
        .setResource(resource)
        .setFullUrl(IdType.newRandomUuid().getValue())
        .setRequest(request);
  }
}
