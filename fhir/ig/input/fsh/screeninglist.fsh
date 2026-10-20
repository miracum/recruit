// This is a simple example of a FSH file.
// This file can be renamed, and additional FSH files can be added.
// SUSHI will look for definitions in any file using the .fsh ending.
Profile: ScreeningList
Id: screening-list
Parent: List
Description: "A profile for the List resource used to represent a list of identified study candidates."
* mode = http://hl7.org/fhir/list-mode#working
* code.coding = CodeSystemScreeningList#screening-recommendations
* extension contains BelongsToStudy named belongsToStudy 1..1

Extension: BelongsToStudy
Id: belongs-to-study
Title: "Clinical study reference"
Description: "A reference to the clinical study that a resource concerns - a List's screened candidates, or a MeasureReport's eligibility attrition funnel"
Context: List, MeasureReport
* value[x] only Reference(RecruitResearchStudy)

CodeSystem: CodeSystemScreeningList
Id: screening-list-type
Title: "recruIT screening list type"
* #screening-recommendations "Screening recommendations"

Instance: ScreeningListIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a screening List's business identifier, which is the study's own identifier - used as the conditional-update key for a study's screening List."
* id = "screening-list-id"
* name = "ScreeningListIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-14"
* uniqueId.type = #uri
* uniqueId.value = "https://miracum.github.io/recruit/fhir/identifiers/screening-list-id"
* uniqueId.preferred = true

Instance: ScreeningListExample
InstanceOf: ScreeningList
Description: "An example of a screening list."
* status = http://hl7.org/fhir/list-status#current
* title = "Test"
* code.coding = CodeSystemScreeningList#screening-recommendations
* extension[BelongsToStudy].valueReference = Reference(RecruitResearchStudyExample) "SAMPLE STUDY"
