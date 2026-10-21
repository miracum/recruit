// This is a simple example of a FSH file.
// This file can be renamed, and additional FSH files can be added.
// SUSHI will look for definitions in any file using the .fsh ending.
Profile: RecruitResearchStudy
Parent: ResearchStudy
Id: recruit-research-study
Description: "A profile for the ResearchStudy resource used to represent a clinical trial."
* enrollment 1..1
* enrollment only Reference(EligibilityGroup)

Instance: RecruitResearchStudyExample
InstanceOf: RecruitResearchStudy
Description: "An example of a recruIT ResearchStudy."
* id = "recruit-research-study-example"
* status = http://hl7.org/fhir/research-study-status#active
* enrollment = Reference(EligibilityGroupExample)

Instance: UICreatedResearchStudyIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a ResearchStudy resource created via the UI."
* id = "ui-created-research-study-id"
* name = "UICreatedResearchStudyIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-14"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/ui-created-research-study-id"
* uniqueId.preferred = true

Instance: SqlCriteriaResearchStudyIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a RecruitResearchStudy whose SQL-encoded criteria are authored as FSH, such as the demo study."
* id = "sql-criteria-research-study-id"
* name = "SqlCriteriaResearchStudyIdentifier"
* status = #active
* kind = #identifier
* date = "2026-10-21"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/sql-criteria-research-study-id"
* uniqueId.preferred = true
