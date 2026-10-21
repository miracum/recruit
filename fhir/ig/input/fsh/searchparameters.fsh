// Expressions must start with the base resource type: HAPI accepts unprefixed expressions but
// silently never indexes them, so searches on them return nothing. A SearchParameter with several
// bases therefore needs a union of one prefixed path per base.
RuleSet: ExtensionReferenceExpression(resourceType, extensionId)
* expression = "{resourceType}.extension('https://openrecruit.dev/fhir/StructureDefinition/{extensionId}').value.ofType(Reference)"

RuleSet: ExtensionReferenceUnionExpression(resourceType1, resourceType2, extensionId)
* expression = "{resourceType1}.extension('https://openrecruit.dev/fhir/StructureDefinition/{extensionId}').value.ofType(Reference) | {resourceType2}.extension('https://openrecruit.dev/fhir/StructureDefinition/{extensionId}').value.ofType(Reference)"

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
* expression = "ResearchStudy.enrollment"
* target = #Group

Instance: GroupCharacteristic
InstanceOf: SearchParameter
Usage: #definition
* id = "group-characteristic"
* name = "GroupCharacteristic"
* description = "An entity referenced in a characteristic"
* base = #Group
* status = #active
// not "characteristic": that's R4's built-in token parameter on Group.characteristic.code, and
// HAPI rejects direct searches on a reference parameter that reuses its code (HAPI-1241)
* code = #characteristic-reference
* type = #reference
* expression = "Group.characteristic.value.ofType(Reference)"

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
* insert ExtensionReferenceUnionExpression(List, MeasureReport, belongs-to-study)
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
* insert ExtensionReferenceExpression(Observation, eligibility-assessment-derived-from-library)
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
