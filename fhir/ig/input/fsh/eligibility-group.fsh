Profile: EligibilityGroup
Parent: Group
Id: eligibility-group
Description: "A profile for the Group resource referenced by a RecruitResearchStudy's enrollment, which defines the study's eligibility criteria as characteristics referencing one or more SQLQueryLibrary criteria."
* identifier 1..*
* actual = false
* code from EligibilityCriteriaTypes (extensible)
* characteristic.value[x] only Reference(SQLQueryLibrary)

CodeSystem: EligibilityCriteriaType
Id: eligibility-criteria-types
Title: "recruIT eligibility criteria type"
Description: "How an eligibility Group's criteria are encoded. Purely descriptive - query-sql-on-fhir discovers studies by their criteria Libraries' type, not by this code."
* #sql-query "SQL query" "The criteria are SQLQueryLibrary resources, each carrying its SQL in one or more dialects."

ValueSet: EligibilityCriteriaTypes
Id: eligibility-criteria-types
Title: "recruIT eligibility criteria types"
Description: "The ways an eligibility Group's criteria may be encoded."
* include codes from system EligibilityCriteriaType

Instance: EligibilityGroupIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for the Group containing references to one or more eligibility criteria Library resources"
* id = "eligibility-group-id"
* name = "EligibilityGroupIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-14"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/eligibility-group-id"
* uniqueId.preferred = true

Instance: EligibilityGroupExample
InstanceOf: EligibilityGroup
Description: "An example of a recruIT eligibility Group with a single inclusion criterion."
* id = "eligibility-group-example"
* identifier.system = "https://openrecruit.dev/fhir/identifiers/eligibility-group-id"
* identifier.value = "example"
* type = #person
* actual = false
* code = EligibilityCriteriaType#sql-query
* characteristic[0].code.text = "All patients"
* characteristic[=].valueReference = Reference(SQLQueryLibraryExample)
* characteristic[=].exclude = false
