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
* code = #characteristic
* type = #reference
* expression = "Group.characteristic.value.ofType(Reference)"

Instance: ListBelongsToStudy
InstanceOf: SearchParameter
Usage: #definition
* id = "list-belongs-to-study"
* name = "ListBelongsToStudy"
* description = "The ResearchStudy the list manages candidates for"
* base = #List
* status = #active
* code = #belongs-to-study
* type = #reference
* expression = "List.extension('https://miracum.github.io/recruit/fhir/StructureDefinition/screening-list-belongs-to-study').value.ofType(Reference)"
* target = #ResearchStudy

Instance: RecruitSearchParametersTransaction
InstanceOf: Bundle
Usage: #definition
* id = "recruit-search-parameters-transaction"
* type = #transaction
* entry[0].resource = ResearchStudyEnrollment
* entry[0].request.method = #PUT
* entry[0].request.url = "SearchParameter/researchstudy-enrollment"
* entry[1].resource = GroupCharacteristic
* entry[1].request.method = #PUT
* entry[1].request.url = "SearchParameter/group-characteristic"
* entry[2].resource = ListBelongsToStudy
* entry[2].request.method = #PUT
* entry[2].request.url = "SearchParameter/list-belongs-to-study"
