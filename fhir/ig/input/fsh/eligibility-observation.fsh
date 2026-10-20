Profile: EligibilityAssessment
Id: eligibility-assessment
Parent: Observation
Title: "recruIT eligibility assessment"
Description: "A per-criterion eligibility Assessment produced by the query engine, indicating whether a single eligibility criterion is met for a ResearchSubject."
* category = EligibilityAssessmentCategory#eligibility-assessment
* extension contains EligibilityAssessmentDerivedFromLibrary named derivedFromLibrary 1..1
* focus 1..1
* focus only Reference(RecruitResearchStudy)
* subject 1..1
* subject only Reference(Patient)
* effectiveDateTime 1..1
* value[x] only CodeableConcept
* valueCodeableConcept from EligibilityAssessmentResult (required)

CodeSystem: EligibilityAssessmentCategory
Id: eligibility-assessment-category
Title: "recruIT eligibility assessment category"
Description: "A custom, non-standard Observation.category code identifying the per-criterion eligibility Assessments produced by the query eligibility engine.."
* #eligibility-assessment "Eligibility assessment"

ValueSet: EligibilityAssessmentResult
Id: eligibility-assessment-result
Title: "recruIT eligibility assessment result"
Description: "The SNOMED CT qualifier values a per-criterion eligibility assessment's value may be set to, indicating whether the criterion is met, not met, unknown, or indeterminate."
* http://snomed.info/sct#373066001 "Yes (qualifier value)"
* http://snomed.info/sct#373067005 "No (qualifier value)"
* http://snomed.info/sct#261665006 "Unknown (qualifier value)"
* http://snomed.info/sct#82334004 "Indeterminate (qualifier value)"

Extension: EligibilityAssessmentDerivedFromLibrary
Id: eligibility-assessment-derived-from-library
Title: "Eligibility assessment derived-from Library"
Description: "A reference to the eligibility-criterion Library resource this assessment - or, on a MeasureReport's attrition-funnel population, this cumulative step - was derived from. Neither Observation.derivedFrom nor MeasureReport.group.population has a slot accepting Library as a target type, so this extension carries the reference instead."
Context: Observation, MeasureReport.group.population
* value[x] only Reference(Library)

Instance: EligibilityAssessmentIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a per-criterion eligibility Observation's business identifier. Derived from the ResearchSubject identifier and the Library identifier of the eligibility criterion."
* id = "eligibility-assessment-id"
* name = "EligibilityAssessmentIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-14"
* uniqueId.type = #uri
* uniqueId.value = "https://miracum.github.io/recruit/fhir/identifiers/eligibility-assessment-id"
* uniqueId.preferred = true
