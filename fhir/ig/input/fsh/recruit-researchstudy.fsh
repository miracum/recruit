// This is a simple example of a FSH file.
// This file can be renamed, and additional FSH files can be added.
// SUSHI will look for definitions in any file using the .fsh ending.
Profile: RecruitResearchStudy
Parent: ResearchStudy
Id: research-study
Description: "A profile for the ResearchStudy resource used to represent a clinical trial."

Instance: RecruitResearchStudyExample
InstanceOf: RecruitResearchStudy
Description: "An example of a recruIT ResearchStudy."
* status = http://hl7.org/fhir/research-study-status#active

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
* uniqueId.value = "https://miracum.github.io/recruit/fhir/identifiers/ui-created-research-study-id"
* uniqueId.preferred = true
