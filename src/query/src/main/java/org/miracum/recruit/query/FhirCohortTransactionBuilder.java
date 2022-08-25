package org.miracum.recruit.query;

import static net.logstash.logback.argument.StructuredArguments.kv;

import ca.uhn.fhir.model.api.TemporalPrecisionEnum;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Function;
import java.util.function.Predicate;
import java.util.stream.Collectors;
import org.apache.commons.lang3.StringUtils;
import org.hl7.fhir.r4.model.Annotation;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Bundle.BundleEntryComponent;
import org.hl7.fhir.r4.model.Bundle.BundleEntryRequestComponent;
import org.hl7.fhir.r4.model.CodeableConcept;
import org.hl7.fhir.r4.model.Coding;
import org.hl7.fhir.r4.model.DateType;
import org.hl7.fhir.r4.model.Device;
import org.hl7.fhir.r4.model.Device.DeviceDeviceNameComponent;
import org.hl7.fhir.r4.model.Device.DeviceNameType;
import org.hl7.fhir.r4.model.Device.DeviceVersionComponent;
import org.hl7.fhir.r4.model.Device.FHIRDeviceStatus;
import org.hl7.fhir.r4.model.Enumerations.AdministrativeGender;
import org.hl7.fhir.r4.model.Extension;
import org.hl7.fhir.r4.model.Identifier;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.Patient;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.hl7.fhir.r4.model.ResearchSubject;
import org.hl7.fhir.r4.model.ResourceType;
import org.hl7.fhir.r4.model.StringType;
import org.miracum.recruit.query.models.CohortDefinition;
import org.miracum.recruit.query.models.Person;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

@Service
public class FhirCohortTransactionBuilder {

  private static final Logger LOG = LoggerFactory.getLogger(FhirCohortTransactionBuilder.class);

  private static final String UUID_URN_PREFIX = "urn:uuid:";
  private static final String MATCH_LABELS_REGEX = "\\[.*]";
  private final FhirSystems systems;
  private final int maxListSize;
  private final boolean shouldNotCreateEncounters;
  private final VisitToEncounterMapper visitToEncounterMapper;
  private final LabelExtractor labelExtractor = new LabelExtractor();
  private final boolean shouldForceUpdateScreeningList;
  private final boolean shouldOnlyCreatePatientsIfNotExist;

  private final CodeableConcept markedAsNoLongerEligibleBySystemConcept;

  @Value("${app.version}")
  private String appVersion;

  public FhirCohortTransactionBuilder(
      FhirSystems fhirSystems,
      @Value("${query.cohortSizeThreshold}") int cohortSizeThreshold,
      @Value("${query.excludePatientParameters.encounter}") boolean shouldNotCreateEncounters,
      @Value("${query.force-update-screening-list}") boolean shouldForceUpdateScreeningList,
      @Value("${query.only-create-patients-if-not-exist}")
          boolean shouldOnlyCreatePatientsIfNotExist,
      VisitToEncounterMapper visitToEncounterMapper) {
    this.systems = fhirSystems;
    this.maxListSize = cohortSizeThreshold;
    this.visitToEncounterMapper = visitToEncounterMapper;
    this.shouldNotCreateEncounters = shouldNotCreateEncounters;
    this.shouldForceUpdateScreeningList = shouldForceUpdateScreeningList;
    this.shouldOnlyCreatePatientsIfNotExist = shouldOnlyCreatePatientsIfNotExist;

    this.markedAsNoLongerEligibleBySystemConcept =
        new CodeableConcept()
            .addCoding(
                new Coding(systems.getSystemDeterminedSubjectStatus(), "ineligible", "Ineligible"))
            .setText(
                "The subject used to be eligible but due to changes to the study criteria or "
                    + "patient record this is no longer the case.");
  }

  private static AdministrativeGender getGenderFromOmop(String gender) {
    if (gender == null) {
      return null;
    }
    switch (gender.toUpperCase()) {
      case "FEMALE":
        return AdministrativeGender.FEMALE;
      case "MALE":
        return AdministrativeGender.MALE;
      case "OTHER":
      case "AMBIGUOUS":
        return AdministrativeGender.OTHER;
      case "UNKNOWN":
      default:
        return AdministrativeGender.UNKNOWN;
    }
  }

  private static DateType parseBirthDate(Person person) {
    DateType date = new DateType();
    // if year of birth is present
    if (person.getYearOfBirth() != null) {
      if (person.getMonthOfBirth() != null) {
        if (person.getDayOfBirth() != null) {
          date.setPrecision(TemporalPrecisionEnum.DAY);
          date.setDay(person.getDayOfBirth());
        } else {
          // no day is available, so the maximum precision is month
          date.setPrecision(TemporalPrecisionEnum.MONTH);
        }
        date.setYear(person.getYearOfBirth().getValue());
        date.setMonth(person.getMonthOfBirth().getValue() - 1);
      } else {
        // no month is available, so the maximum precision is year
        date.setPrecision(TemporalPrecisionEnum.YEAR);
        date.setYear(person.getYearOfBirth().getValue());
      }
    }
    return date;
  }

  // via https://www.baeldung.com/java-streams-distinct-by
  private static <T> Predicate<T> distinctByKey(Function<? super T, ?> keyExtractor) {

    Map<Object, Boolean> seen = new ConcurrentHashMap<>();
    return t -> seen.putIfAbsent(keyExtractor.apply(t), Boolean.TRUE) == null;
  }

  public Bundle buildFromOmopCohort(
      CohortDefinition cohort, List<Person> personsInCohort, long actualCohortSize) {
    return buildFromOmopCohort(
        cohort,
        personsInCohort,
        actualCohortSize,
        new ScreeningListResources(new ListResource(), List.of(), List.of()));
  }

  /**
   * Builds an FHIR Transaction with a list of ResearchSubjects from a given OMOP cohort includes
   * Patients, ResearchStudy, ResearchSubjects, List
   *
   * @param cohort OMOP CohortDefinition from an existing cohort
   * @param personsInCohort persons who should be packed to screening list
   * @param actualCohortSize actual number of persons in OMOP cohort (may be larger than
   *     personsInCohort.size)
   * @param previousScreeningListResources the previous instance of the screening list and a list of
   *     all Patients and ResearchSubjects associated with the ResearchStudy represented by the
   *     current cohort
   * @return Bundle of type transaction
   */
  public Bundle buildFromOmopCohort(
      CohortDefinition cohort,
      List<Person> personsInCohort,
      long actualCohortSize,
      ScreeningListResources previousScreeningListResources) {
    if (previousScreeningListResources == null) {
      throw new IllegalArgumentException("previousScreeningListResources may not be null");
    }

    var cohortId = cohort.getId();

    // Search in description and then in the cohort name for a study acronym
    var acronym = labelExtractor.extractByTag("acronym", cohort.getDescription());
    if (acronym == null) {
      acronym = labelExtractor.extractByTag("acronym", cohort.getName());
    }

    // if all else fails, use the cohort name as the study acronym
    if (acronym == null) {
      acronym = cohort.getName().replaceAll(MATCH_LABELS_REGEX, "");
    }

    acronym = acronym.trim();

    // create BUNDLE
    Bundle transaction = new Bundle().setType(Bundle.BundleType.TRANSACTION);

    var device = createDeviceBundleEntryComponent();
    transaction.addEntry(device);
    var deviceReference =
        new Reference(device.getFullUrl()).setDisplay("recruIT query module " + appVersion);

    // create RESEARCHSTUDY and add to bundle
    ResearchStudy study = createResearchStudy(cohort, acronym);
    UUID studyUuid = UUID.randomUUID();
    transaction.addEntry(createStudyBundleEntryComponent(study, cohortId, studyUuid));

    // create SCREENINGLIST
    ListResource screeningList = createScreeningList(cohortId, studyUuid, acronym, deviceReference);
    if (actualCohortSize > personsInCohort.size()) {
      screeningList.addNote(
          new Annotation()
              .setAuthor(new StringType("UC1-Query Module"))
              .setText(
                  "Es wurden mehr passende Patienten gefunden als auf dieser Liste dargestellt werden können (insgesamt "
                      + actualCohortSize
                      + "). Nur die ersten "
                      + this.maxListSize
                      + " Vorschläge werden angezeigt."));
    }

    var previousList = previousScreeningListResources.getScreeningList();
    var patientsInPreviousList = previousScreeningListResources.getPatients();
    var subjectsInPreviousList = previousScreeningListResources.getResearchSubjects();

    var patientsInCohort = new ArrayList<Patient>();

    // LOOP through all OMOP Persons
    for (Person personInCohort : personsInCohort) {
      // create PATIENT with OMOP ID as an Identifier and add to bundle
      var patient = createPatient(personInCohort);
      patientsInCohort.add(patient);
      // TODO: may be replaced using: new Reference(IdType.newRandomUuid());
      var patientUuid = UUID.randomUUID();
      transaction.addEntry(createPatientBundleEntryComponent(patient, patientUuid));
      // create RESEARCHSUBJECT with Reference on Patient and Study and add to bundle
      var researchSubject = createResearchSubject(studyUuid, patientUuid, study, patient);
      var subjectUuid = UUID.randomUUID();
      transaction.addEntry(
          createResearchSubjectBundleEntryComponent(
              researchSubject,
              subjectUuid,
              patient.getIdentifierFirstRep(),
              study.getIdentifierFirstRep()));

      if (!shouldNotCreateEncounters) {
        var patientReference = new Reference(UUID_URN_PREFIX + patientUuid);
        var visitOccurrences = personInCohort.getVisitOccurrences();
        var encounterBundleEntries = visitToEncounterMapper.map(visitOccurrences, patientReference);
        for (var encounterBundleEntry : encounterBundleEntries) {
          transaction.addEntry(encounterBundleEntry);
        }
      } else {
        LOG.debug(
            "Creation of Encounter resources is disabled. Transaction will only include ResearchStudy,"
                + " ResearchSubject, and Patient resources.");
      }

      // add to the new screening list only if it doesn't already exist
      // note that we unfortunately can't (yet) just check List.entry since they
      // just reference the ResearchSubject and we can't easily determine if these subjects
      // correspond to an existing Patient.
      // These comparisons are all but efficient since we are comparing each entry in the cohort
      // against
      // all entries in the list of patients that were previously in the screening list, so O(n²).
      // However, since we are usually looking at patient numbers in the lower tens or hundreds at
      // most,
      // this shouldn't be too limiting in reality.
      var matchesPatientAlreadyOnTheList =
          patientsInPreviousList.stream()
              .anyMatch(existing -> checkIfPatientsMatchByIdentifier(existing, patient));

      // note that even if the recommendation isn't new, we still update the Patient resources and
      // their Encounters for all persons in the current cohort generation.
      var formattedIdentifier =
          String.format(
              "%s|%s",
              patient.getIdentifierFirstRep().getSystem(),
              patient.getIdentifierFirstRep().getValue());
      if (!matchesPatientAlreadyOnTheList) {
        LOG.debug(
            "Adding Patient ({}) to screening list", kv("patientIdentifier", formattedIdentifier));
        screeningList.addEntry(
            new ListResource.ListEntryComponent()
                .setItem(new Reference(UUID_URN_PREFIX + subjectUuid))
                .setDate(new Date()));
      } else {
        LOG.debug(
            "Patient ({}) is already on the list, not adding.",
            kv("patientIdentifier", formattedIdentifier));
      }
    }

    // TODO: instead of maps, we could resolve the references when fetching them from the server
    //  and use ResearchSubject.getIndividualTarget() to get the patient referenced by the subject.
    var patientIdToResourceMap =
        patientsInPreviousList.stream()
            .filter(distinctByKey(patient -> patient.getIdElement().getIdPart()))
            .collect(Collectors.toMap(patient -> patient.getIdElement().getIdPart(), item -> item));

    // given a ResearchSubject.id, returns the Patient resource it is referencing
    var researchSubjectIdToPatientMap =
        subjectsInPreviousList.stream()
            .filter(distinctByKey(subject -> subject.getIdElement().getIdPart()))
            .collect(
                Collectors.toMap(
                    subject -> subject.getIdElement().getIdPart(),
                    subject ->
                        patientIdToResourceMap.get(
                            subject.getIndividual().getReferenceElement().getIdPart())));

    // if any of the List entries flags were cleared, this indicates a need to send an updated
    // screening list to the server - even if the number of subjects in the list itself hasn't
    // changed
    var haveFlagsBeenUpdated = false;

    // add all List.entry to the newly created screening list - these entries are definitely part
    // of the update.
    for (var entry : previousList.getEntry()) {
      // in previous versions of the query module, List.entry.date was never set.
      // For consistency, we set it here for the first time - even if the
      // actual recommendation date maybe much older.
      // we could use the cohort entry date here as well
      if (!entry.hasDate()) {
        entry.setDate(new Date());
      }

      // get the Patient resource corresponding to the current list entry
      var existingPatient =
          researchSubjectIdToPatientMap.get(entry.getItem().getReferenceElement().getIdPart());
      if (existingPatient == null) {
        LOG.error(
            "Unable to find the Patient resource referenced by List entry {}",
            kv("entryItemReference", entry.getItem().getReference()));
      } else {
        // check if the Patient referenced by the current entry in this previous screening list
        // is also a Patient that is part of the newly generated Person cohort from OMOP
        var matchesPatientThatIsPartOfGeneratedCohort =
            patientsInCohort.stream()
                .anyMatch(
                    cohortPatient ->
                        checkIfPatientsMatchByIdentifier(cohortPatient, existingPatient));

        if (matchesPatientThatIsPartOfGeneratedCohort) {
          LOG.debug(
              "Patient that was already on the previous screening "
                  + "list is also included in the newly generated cohort.");
          // because the Patient may drop from the list in one run (due to changed criteria),
          // but may be re-added in another run, we can't forget to clear any flags.
          if (entry.hasFlag()) {
            LOG.debug("List entry has a flag set. Clearing it.");
            entry.setFlag(null);
            haveFlagsBeenUpdated = true;
          }
        } else {
          // the Patient that was on the previous list is no longer part of the newly generated
          // cohort, we have to flag it as such.
          entry.setFlag(markedAsNoLongerEligibleBySystemConcept);
          haveFlagsBeenUpdated = true;
        }
      }

      // all entries of the previous screening list will be added to the newly created one.
      // previousList could be empty, though.
      screeningList.addEntry(entry);
    }

    // in general, only add the list and update its contents if the number of recommendations has
    // increased
    // but for convenience, we also add and update a list if it is empty - this makes sure the list
    // shows on in the web UI every time. Note that we could be smarter here and check if this
    // is the first time the list was published to avoid constantly updating an empty list.
    if (shouldForceUpdateScreeningList
        || haveFlagsBeenUpdated
        || screeningList.getEntry().isEmpty()
        || screeningList.getEntry().size() > previousList.getEntry().size()) {
      LOG.debug(
          "Adding screening list to transaction: either the contents changed, it is empty,"
              + " or the update was forced.");
      transaction.addEntry(createListBundleEntryComponent(screeningList));
    }

    return transaction;
  }

  public Identifier getScreeningListIdentifierFromCohortId(Long cohortId) {
    var identifierValue = "screeninglist-" + cohortId;
    return new Identifier()
        .setSystem(systems.getScreeningListIdentifier())
        .setValue(identifierValue);
  }

  // ------------------PRIVATE HELPER METHODS-------------------------//

  private BundleEntryComponent createListBundleEntryComponent(ListResource screeningList) {
    assert screeningList.getIdentifier().size() == 1;

    var listIdentifier = screeningList.getIdentifierFirstRep();
    return new BundleEntryComponent()
        .setResource(screeningList)
        .setFullUrl(UUID_URN_PREFIX + UUID.randomUUID())
        .setRequest(
            new BundleEntryRequestComponent()
                .setMethod(Bundle.HTTPVerb.PUT)
                .setUrl(
                    "List?identifier="
                        + listIdentifier.getSystem()
                        + "|"
                        + listIdentifier.getValue()));
  }

  private Patient createPatient(Person personInCohort) {
    var patient =
        new Patient()
            .setBirthDateElement(parseBirthDate(personInCohort))
            .setGender(getGenderFromOmop(personInCohort.getGender()));
    if (StringUtils.isNotEmpty(personInCohort.getSourceId())) {
      patient.addIdentifier(
          new Identifier()
              .setSystem(systems.getPatientId())
              .setValue(personInCohort.getSourceId())
              .setType(
                  new CodeableConcept()
                      .addCoding(
                          new Coding().setSystem(systems.getIdentifierType()).setCode("MR"))));
    }
    patient.addIdentifier(
        new Identifier()
            .setSystem(systems.getOmopSubjectIdentifier())
            .setValue(personInCohort.getPersonId().toString()));
    return patient;
  }

  private BundleEntryComponent createPatientBundleEntryComponent(
      Patient patient, UUID patientUuid) {
    var request = new BundleEntryRequestComponent();

    if (shouldOnlyCreatePatientsIfNotExist) {
      request
          .setMethod(Bundle.HTTPVerb.POST)
          .setUrl("Patient")
          .setIfNoneExist(
              "identifier="
                  + patient.getIdentifierFirstRep().getSystem()
                  + "|"
                  + patient.getIdentifierFirstRep().getValue());
    } else {
      request
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(
              "Patient?identifier="
                  + patient.getIdentifierFirstRep().getSystem()
                  + "|"
                  + patient.getIdentifierFirstRep().getValue());
    }

    return new BundleEntryComponent()
        .setResource(patient)
        .setFullUrl(UUID_URN_PREFIX + patientUuid)
        .setRequest(request);
  }

  private ResearchStudy createResearchStudy(CohortDefinition cohort, String acronym) {
    var study =
        new ResearchStudy()
            .setStatus(ResearchStudy.ResearchStudyStatus.ACTIVE)
            .addIdentifier(
                new Identifier()
                    .setSystem(systems.getOmopCohortIdentifier())
                    .setValue(cohort.getId().toString()));
    study.getMeta().setSource(systems.getStudySource());

    if (cohort.getName() != null) {
      var title = cohort.getName().replaceAll(MATCH_LABELS_REGEX, "").trim();
      study.setTitle(title);
    }

    if (cohort.getDescription() != null) {
      var description = cohort.getDescription().replaceAll(MATCH_LABELS_REGEX, "").trim();
      study.setDescription(description);
    }

    if (acronym != null) {
      study.addExtension(systems.getResearchStudyAcronym(), new StringType(acronym));
    }

    return study;
  }

  // CREATE RESOURCES
  private ResearchSubject createResearchSubject(
      UUID studyUUID, UUID patientUuid, ResearchStudy study, Patient patient) {
    return new ResearchSubject()
        .setStatus(ResearchSubject.ResearchSubjectStatus.CANDIDATE)
        .setStudy(
            new Reference(UUID_URN_PREFIX + studyUUID).setIdentifier(study.getIdentifierFirstRep()))
        .setIndividual(
            new Reference(UUID_URN_PREFIX + patientUuid)
                .setIdentifier(patient.getIdentifierFirstRep()));
  }

  private BundleEntryComponent createResearchSubjectBundleEntryComponent(
      ResearchSubject researchSubject, UUID subjectUuid, Identifier patientId, Identifier studyId) {
    return new BundleEntryComponent()
        .setResource(researchSubject)
        .setFullUrl(UUID_URN_PREFIX + subjectUuid)
        .setRequest(
            new BundleEntryRequestComponent()
                .setMethod(Bundle.HTTPVerb.POST)
                // we can't easily use `ResearchSubject?identifier=` here (yet?) since this will
                // cause duplicate
                // subjects to be created if ones already exist that were created with a previous
                // version
                // where `ResearchSubject.identifier` was left empty.
                .setIfNoneExist(
                    "ResearchSubject?patient.identifier="
                        + patientId.getSystem()
                        + "|"
                        + patientId.getValue()
                        + "&"
                        + "study.identifier="
                        + studyId.getSystem()
                        + "|"
                        + studyId.getValue())
                .setUrl(ResourceType.ResearchSubject.name()));
  }

  private ListResource createScreeningList(
      Long cohortId, UUID studyUuid, String acronym, Reference deviceReference) {

    var identifier = getScreeningListIdentifierFromCohortId(cohortId);

    var list =
        new ListResource()
            .setStatus(ListResource.ListStatus.CURRENT)
            .setMode(ListResource.ListMode.WORKING)
            .setTitle(String.format("Screening list for the '%s' study", acronym))
            .setSource(deviceReference)
            .setOrderedBy(
                new CodeableConcept()
                    .addCoding(new Coding(systems.getListOrder(), "system", "Sorted by System")))
            .setCode(
                new CodeableConcept()
                    .addCoding(
                        new Coding()
                            .setSystem(systems.getScreeningListCoding())
                            .setCode("screening-recommendations")))
            .addIdentifier(identifier);

    // add Study to screening list as an extension
    var studyReference = new Reference(UUID_URN_PREFIX + studyUuid).setDisplay(acronym);
    list.addExtension(
        new Extension()
            .setUrl(systems.getScreeningListStudyReferenceExtension())
            .setValue(studyReference));
    return list;
  }

  private BundleEntryComponent createDeviceBundleEntryComponent() {
    var identifier =
        new Identifier().setSystem(systems.getDeviceId()).setValue("query-" + appVersion);

    var deviceNames =
        List.of(
            new DeviceDeviceNameComponent()
                .setName("query")
                .setType(DeviceNameType.MANUFACTURERNAME),
            new DeviceDeviceNameComponent()
                .setName("recruIT Query Module")
                .setType(DeviceNameType.USERFRIENDLYNAME));

    var deviceVersion = new DeviceVersionComponent().setValue(appVersion);

    var device =
        new Device()
            .setIdentifier(List.of(identifier))
            .setStatus(FHIRDeviceStatus.ACTIVE)
            .setManufacturer("miracum.org")
            .setDeviceName(deviceNames)
            .setVersion(List.of(deviceVersion));

    return new BundleEntryComponent()
        .setResource(device)
        .setFullUrl(UUID_URN_PREFIX + UUID.randomUUID())
        .setRequest(
            new BundleEntryRequestComponent()
                .setMethod(Bundle.HTTPVerb.POST)
                .setIfNoneExist(
                    String.format(
                        "identifier=%s|%s", identifier.getSystem(), identifier.getValue()))
                .setUrl(ResourceType.Device.name()));
  }

  private BundleEntryComponent createStudyBundleEntryComponent(
      ResearchStudy study, Long cohortId, UUID studyUuid) {
    return new BundleEntryComponent()
        .setResource(study)
        .setFullUrl(UUID_URN_PREFIX + studyUuid)
        .setRequest(
            new BundleEntryRequestComponent()
                .setMethod(Bundle.HTTPVerb.PUT)
                .setUrl(
                    "ResearchStudy?identifier="
                        + systems.getOmopCohortIdentifier()
                        + "|"
                        + cohortId));
  }

  // checks if any of the identifiers of "a" matches any of the identifiers of "b"
  private boolean checkIfPatientsMatchByIdentifier(Patient a, Patient b) {
    var allIdentifiersOfBoth = new ArrayList<>(a.getIdentifier());
    allIdentifiersOfBoth.addAll(b.getIdentifier());

    // we first convert the identifiers to a "<system>|<value>" representation
    // which allows us to use the toSet collector, which can easily compare
    // strings for their equality. An alternative solution would be to
    // use Identifier::equalsShallow for some custom toSet implementation.
    var distinctIdentifiers =
        allIdentifiersOfBoth.stream()
            .map(
                identifier -> String.format("%s|%s", identifier.getSystem(), identifier.getValue()))
            .collect(Collectors.toSet());

    // if the number of distinct identifiers is less than the number
    // of all identifiers, we know that there was at least one duplicate
    return distinctIdentifiers.size() < allIdentifiersOfBoth.size();
  }
}
