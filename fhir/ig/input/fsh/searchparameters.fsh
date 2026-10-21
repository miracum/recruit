RuleSet: ExtensionReferenceExpression(extensionId)
* expression = "extension('https://openrecruit.dev/fhir/StructureDefinition/{extensionId}').value.ofType(Reference)"

RuleSet: PutEntry(resource, url)
* entry[+].resource = {resource}
* entry[=].request.method = #PUT
* entry[=].request.url = "{url}"

Instance: ResearchStudyEnrollment
InstanceOf: SearchParameter
Usage: #definition
* id = "researchstudy-enrollment"
* name = "ResearchStudyEnrollment"
* description = "The Group resource representing the study enrollment"
* base = #ResearchStudy
* status = #active
* code = #enrollment
* type = #reference
* expression = "enrollment"
* target = #Group

Instance: GroupCharacteristic
InstanceOf: SearchParameter
Usage: #definition
* id = "group-characteristic"
* name = "GroupCharacteristic"
* description = "An entity referenced in a characteristic"
* base = #Group
* status = #active
* code = #characteristic
* type = #reference
* expression = "characteristic.value.ofType(Reference)"

Instance: SearchParameterBelongsToStudy
InstanceOf: SearchParameter
Usage: #definition
* id = "belongs-to-study"
* name = "BelongsToStudy"
* description = "The ResearchStudy a List or MeasureReport pertains to"
* base = #List
* base[+] = #MeasureReport
* status = #active
* code = #belongs-to-study
* type = #reference
* insert ExtensionReferenceExpression(belongs-to-study)
* target = #ResearchStudy

Instance: ObservationDerivedFromLibrary
InstanceOf: SearchParameter
Usage: #definition
* id = "observation-derived-from-library"
* name = "ObservationDerivedFromLibrary"
* description = "The Library resource containing the eligibility criterion this Observation was derived from"
* base = #Observation
* status = #active
* code = #derived-from-library
* type = #reference
* insert ExtensionReferenceExpression(eligibility-assessment-derived-from-library)
* target = #Library

Instance: RecruitSearchParametersTransaction
InstanceOf: Bundle
Usage: #definition
* id = "recruit-search-parameters-transaction"
* type = #transaction
* insert PutEntry(ResearchStudyEnrollment, SearchParameter/researchstudy-enrollment)
* insert PutEntry(GroupCharacteristic, SearchParameter/group-characteristic)
* insert PutEntry(SearchParameterBelongsToStudy, SearchParameter/belongs-to-study)
* insert PutEntry(ObservationDerivedFromLibrary, SearchParameter/observation-derived-from-library)
