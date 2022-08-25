package org.miracum.recruit.query;

import static net.logstash.logback.argument.StructuredArguments.kv;

import com.github.slugify.Slugify;
import com.google.common.base.Strings;
import java.sql.Date;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Bundle.BundleEntryComponent;
import org.hl7.fhir.r4.model.Bundle.BundleEntryRequestComponent;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.Coding;
import org.hl7.fhir.r4.model.Encounter;
import org.hl7.fhir.r4.model.Encounter.EncounterStatus;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.Period;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.ResourceType;
import org.miracum.recruit.query.models.VisitDetail;
import org.miracum.recruit.query.models.VisitOccurrence;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

@Service
public class VisitToEncounterMapper {

  private static final Logger LOG = LoggerFactory.getLogger(VisitToEncounterMapper.class);

  private static final Integer VISIT_TYPE_CONCEPT_STILL_PATIENT = 32220;
  private static final Integer VISIT_CONCEPT_IN_PATIENT = 9201;
  private static final Integer VISIT_CONCEPT_OUT_PATIENT = 9202;
  private static final Integer VISIT_CONCEPT_EMERGENCY_ROOM = 9203;

  private final Coding impCoding;
  private final FhirSystems fhirSystems;
  private final Map<Integer, Coding> visitConceptToEncounterClassMap;
  private final Slugify slugify = Slugify.builder().build();

  public VisitToEncounterMapper(FhirSystems fhirSystems) {
    this.fhirSystems = fhirSystems;
    this.impCoding = new Coding(fhirSystems.getActEncounterCode(), "IMP", "inpatient encounter");

    visitConceptToEncounterClassMap = new HashMap<>();
    visitConceptToEncounterClassMap.put(VISIT_CONCEPT_IN_PATIENT, impCoding);
    visitConceptToEncounterClassMap.put(
        VISIT_CONCEPT_OUT_PATIENT,
        new Coding(fhirSystems.getActEncounterCode(), "AMB", "ambulatory"));
    visitConceptToEncounterClassMap.put(
        VISIT_CONCEPT_EMERGENCY_ROOM,
        new Coding(fhirSystems.getActEncounterCode(), "EMER", "emergency"));
  }

  private static Reference createReferenceWithDisplay(String display) {
    return new Reference().setType(ResourceType.Location.name()).setDisplay(display);
  }

  public Collection<BundleEntryComponent> map(
      Collection<VisitOccurrence> visitOccurrences, Reference patientReference) {
    if (visitOccurrences == null) {
      return Collections.emptyList();
    }
    return visitOccurrences.stream()
        .flatMap(v -> map(v, patientReference).stream())
        .collect(Collectors.toList());
  }

  private Collection<BundleEntryComponent> map(
      VisitOccurrence visitOccurrence, Reference patientReference) {
    if (Strings.isNullOrEmpty(visitOccurrence.getVisitSourceValue())) {
      LOG.error(
          "Visit Occurrence {} does not have its source_value set. Not processing.",
          kv("visitOccurrenceId", visitOccurrence.getVisitOccurrenceId()));
      return Collections.emptyList();
    }

    var result = new ArrayList<BundleEntryComponent>();

    var mainEncounterEntryComponent =
        mapVisitOccurrenceToMainEncounter(visitOccurrence, patientReference);

    result.add(mainEncounterEntryComponent);

    var mainEncounterReference = new Reference(mainEncounterEntryComponent.getFullUrl());

    for (var visitDetail : visitOccurrence.getVisitDetails()) {
      var subEncounter =
          mapVisitDetailToSubEncounter(
              visitDetail, visitOccurrence, patientReference, mainEncounterReference);

      subEncounter.ifPresent(result::add);
    }

    return result;
  }

  private BundleEntryComponent mapVisitOccurrenceToMainEncounter(
      VisitOccurrence visitOccurrence, Reference patientReference) {

    var mainEncounterFullUrl = IdType.newRandomUuid().getValue();

    // both status and class are required fields so they should be filled as soon as possible
    // to ensure validation passes.
    var mainEncounter = new Encounter().setStatus(EncounterStatus.UNKNOWN).setClass_(impCoding);
    var period = new Period();

    if (visitOccurrence.getVisitStartDate() != null) {
      period.setStart(Date.valueOf(visitOccurrence.getVisitStartDate()));
    } else {
      LOG.debug(
          "visit start date not set for {}",
          kv("visitSourceValue", visitOccurrence.getVisitSourceValue()));
    }

    if (visitOccurrence.getVisitTypeConceptId() != null) {
      if (visitOccurrence.getVisitTypeConceptId().equals(VISIT_TYPE_CONCEPT_STILL_PATIENT)) {
        mainEncounter.setStatus(EncounterStatus.INPROGRESS);
      } else {
        mainEncounter.setStatus(EncounterStatus.FINISHED);
        if (visitOccurrence.getVisitEndDate() != null) {
          period.setEnd(Date.valueOf(visitOccurrence.getVisitEndDate()));
        }
      }
    }

    mainEncounter.setPeriod(period);

    var encounterClass =
        visitConceptToEncounterClassMap.getOrDefault(
            visitOccurrence.getVisitConceptId(), impCoding);
    mainEncounter.setClass_(encounterClass);

    mainEncounter
        .addIdentifier()
        .setSystem(fhirSystems.getEncounterId())
        .setType(
            new CodeableConcept()
                .addCoding(new Coding().setSystem(fhirSystems.getIdentifierType()).setCode("VN")))
        .setValue(visitOccurrence.getVisitSourceValue());

    mainEncounter.setSubject(patientReference);

    if (visitOccurrence.getCareSite() != null) {
      var locationReference =
          createReferenceWithDisplay(visitOccurrence.getCareSite().getCareSiteName());
      mainEncounter.addLocation().setLocation(locationReference);
    }

    return new BundleEntryComponent()
        .setResource(mainEncounter)
        .setFullUrl(mainEncounterFullUrl)
        .setRequest(
            new BundleEntryRequestComponent()
                .setMethod(Bundle.HTTPVerb.POST)
                .setIfNoneExist(
                    String.format(
                        "identifier=%s|%s",
                        mainEncounter.getIdentifierFirstRep().getSystem(),
                        mainEncounter.getIdentifierFirstRep().getValue()))
                .setUrl(mainEncounter.getResourceType().name()));
  }

  private Optional<BundleEntryComponent> mapVisitDetailToSubEncounter(
      VisitDetail visitDetail,
      VisitOccurrence visitOccurrence,
      Reference patientReference,
      Reference mainEncounterReference) {

    if (visitDetail.getVisitDetailStartDate() == null) {
      LOG.warn(
          "unable to map visit_detail ({} belonging to {}) to an Encounter resource: "
              + "visit_detail_start_date is missing.",
          kv("visitDetailId", visitDetail.getVisitDetailId()),
          kv("visitOccurrenceId", visitDetail.getVisitOccurrenceId()));
      return Optional.empty();
    }

    if (visitDetail.getCareSite() == null && visitDetail.getVisitDetailSourceValue() == null) {
      LOG.warn(
          "unable to map visit_detail ({} belonging to {}) to an Encounter resource: "
              + "both visit_detail_care_site_id and visit_detail_source_value are missing.",
          kv("visitDetailId", visitDetail.getVisitDetailId()),
          kv("visitOccurrenceId", visitDetail.getVisitOccurrenceId()));
      return Optional.empty();
    }

    var subEncounter = new Encounter().setStatus(EncounterStatus.UNKNOWN).setClass_(impCoding);

    var period = new Period();

    if (visitDetail.getVisitDetailStartDate() != null) {
      period.setStart(Date.valueOf(visitDetail.getVisitDetailStartDate()));
    } else {
      LOG.debug(
          "visit start date not set for {} and {}",
          kv("visitSourceValue", visitOccurrence.getVisitSourceValue()),
          kv("visitDetailSourceValue", visitDetail.getVisitDetailSourceValue()));
    }

    if (visitDetail.getVisitDetailTypeConceptId() != null) {
      if (visitDetail.getVisitDetailTypeConceptId().equals(VISIT_TYPE_CONCEPT_STILL_PATIENT)) {
        subEncounter.setStatus(EncounterStatus.INPROGRESS);
      } else {
        subEncounter.setStatus(EncounterStatus.FINISHED);
        if (visitDetail.getVisitDetailEndDate() != null) {
          period.setEnd(Date.valueOf(visitDetail.getVisitDetailEndDate()));
        }
      }
    }

    subEncounter.setPeriod(period);

    var encounterClass =
        visitConceptToEncounterClassMap.getOrDefault(
            visitDetail.getVisitDetailTypeConceptId(), impCoding);
    subEncounter.setClass_(encounterClass);

    // construct the display value for the referenced Location
    var referenceDisplayBuilder = new StringBuilder();
    if (visitDetail.getCareSite() != null) {
      referenceDisplayBuilder.append(visitDetail.getCareSite().getCareSiteName());
      if (visitDetail.getVisitDetailSourceValue() != null) {
        referenceDisplayBuilder.append(
            String.format(" (%s)", visitDetail.getVisitDetailSourceValue()));
      }

    } else {
      referenceDisplayBuilder.append(visitDetail.getVisitDetailSourceValue());
    }

    var locationReference = createReferenceWithDisplay(referenceDisplayBuilder.toString());

    subEncounter.addLocation().setLocation(locationReference);

    // the visit_detail table does not contain stable identification data as opposed to the
    // visit_occurrence's visit_source_value. So we'll have to create a surrogate identifier
    // from the visit_source_value, the visit_detail's start date and the visit_detail_source_value
    // which should at least approximate the location of the visit to some extent.

    var identifierValueBuilder = new StringBuilder();
    identifierValueBuilder.append(visitOccurrence.getVisitSourceValue());

    // if only the care_site is set, use it to construct the identifier
    identifierValueBuilder.append("-");
    identifierValueBuilder.append(visitDetail.getVisitDetailStartDate());

    if (visitDetail.getCareSite() != null) {
      identifierValueBuilder.append("-");
      identifierValueBuilder.append(visitDetail.getCareSite().getCareSiteName());
    }

    if (!Strings.isNullOrEmpty(visitDetail.getVisitDetailSourceValue())) {
      identifierValueBuilder.append("-");
      identifierValueBuilder.append(visitDetail.getVisitDetailSourceValue());
    }

    var identifierValue = slugify.slugify(identifierValueBuilder.toString());

    subEncounter.setSubject(patientReference);
    subEncounter.setPartOf(mainEncounterReference);
    subEncounter
        .addIdentifier()
        .setType(
            new CodeableConcept()
                .addCoding(new Coding().setSystem(fhirSystems.getIdentifierType()).setCode("VN")))
        .setSystem(fhirSystems.getSubEncounterId())
        .setValue(identifierValue);

    var entryComponent =
        new BundleEntryComponent()
            .setResource(subEncounter)
            .setFullUrl(IdType.newRandomUuid().getValue())
            .setRequest(
                new BundleEntryRequestComponent()
                    .setMethod(Bundle.HTTPVerb.POST)
                    .setIfNoneExist(
                        String.format(
                            "identifier=%s|%s",
                            subEncounter.getIdentifierFirstRep().getSystem(),
                            subEncounter.getIdentifierFirstRep().getValue()))
                    .setUrl(subEncounter.getResourceType().name()));

    return Optional.of(entryComponent);
  }
}
