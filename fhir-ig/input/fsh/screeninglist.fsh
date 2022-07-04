// This is a simple example of a FSH file.
// This file can be renamed, and additional FSH files can be added.
// SUSHI will look for definitions in any file using the .fsh ending.
Profile: ScreeningList
Id: screening-list
Parent: List
Description: "A profile for the List resource used to represent a list of identified study candidates."
* mode = http://hl7.org/fhir/list-mode#working
* code.coding = CodeSystemScreeningList#screening-recommendations
* extension contains ScreeningListBelongsToStudy named belongsToStudy 1..1

Extension: ScreeningListBelongsToStudy
Id: screening-list-belongs-to-study
Title: "Screening list clinical study reference"
Description: "A reference to the clinical study that this screening list contains study candidates for"
* value[x] only Reference(RecruitResearchStudy)

CodeSystem: CodeSystemScreeningList
Id: screening-list-type
Title: "CodeSystem - recruIT screening list type"
* #screening-recommendations "Screening recommendations"

Instance: ScreeningListExample
InstanceOf: ScreeningList
Description: "An example of a screening list."
* status = http://hl7.org/fhir/list-status#current
* title = "Test"
* code.coding = CodeSystemScreeningList#screening-recommendations
* extension[ScreeningListBelongsToStudy].valueReference = Reference(RecruitResearchStudyExample) "SAMPLE STUDY"
