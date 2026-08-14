CodeSystem: EligibilityObservationCategory
Id: eligibility-observation-category
Title: "recruIT eligibility observation category"
Description: "A custom, non-standard Observation.category code identifying the synthetic per-criterion eligibility Observations produced by the sql-on-fhir eligibility engine, distinguishing them from genuine clinical Observations elsewhere in the system."
* #eligibility-assessment "Eligibility assessment"

Extension: EligibilityObservationDerivedFromLibrary
Id: eligibility-observation-derived-from-library
Title: "Eligibility observation derived-from Library"
Description: "A reference to the eligibility-criterion Library resource this Observation was derived from. Observation.derivedFrom does not accept Library as a target type, so this extension carries the reference instead."
Context: Observation
* value[x] only Reference(Library)

Instance: EligibilityObservationIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a per-criterion eligibility Observation's business identifier"
* id = "eligibility-observation-id"
* name = "EligibilityObservationIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-14"
* uniqueId.type = #uri
* uniqueId.value = "https://miracum.github.io/recruit/fhir/identifiers/eligibility-observation-id"
* uniqueId.preferred = true
