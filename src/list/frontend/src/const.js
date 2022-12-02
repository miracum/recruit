export default {
  SYSTEM_SUBJECT_IDENTIFIER: "http://ohdsi.org/omop/fhir/subject-identifier",
  SYSTEM_STUDY_ACRONYM: "https://fhir.miracum.org/uc1/StructureDefinition/studyAcronym",
  SYSTEM_SCREENING_LIST: "https://fhir.miracum.org/uc1/CodeSystem/screeningList",
  SYSTEM_IDENTIFIER_TYPE: "http://terminology.hl7.org/CodeSystem/v2-0203",
  SYSTEM_DETERMINED_SUBJECT_STATUS: "https://fhir.miracum.org/uc1/CodeSystem/system-determined-subject-status",
  URL_NOTE_EXTENSION: "https://fhir.miracum.org/uc1/StructureDefinition/researchSubjectNote",
  // make sure to also update this in server/fhirAccessFilter.js
  URL_LIST_BELONGS_TO_STUDY_EXTENSION: "https://fhir.miracum.org/uc1/StructureDefinition/belongsToStudy",
  STATUS_TRANSLATION: {
    candidate: "Rekrutierungsvorschlag",
    screening: "Wird geprüft",
    eligible: "Erfüllt E/A-Kriterien",
    ineligible: "E/A-Kriterien nicht erfüllt",
    "on-study": "Wurde eingeschlossen",
    withdrawn: "Nicht gewillt teilzunehmen",
  },
};
