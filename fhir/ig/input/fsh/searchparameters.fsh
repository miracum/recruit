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
* expression = "List.extension('https://miracum.github.io/recruit/fhir/StructureDefinition/belongs-to-study').value.ofType(Reference)"
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
* expression = "Observation.extension('https://miracum.github.io/recruit/fhir/StructureDefinition/eligibility-assessment-derived-from-library').value.ofType(Reference)"
* target = #Library

Instance: MeasureReportBelongsToStudy
InstanceOf: SearchParameter
Usage: #definition
* id = "measurereport-belongs-to-study"
* name = "MeasureReportBelongsToStudy"
* description = "The ResearchStudy a MeasureReport's eligibility attrition funnel pertains to"
* base = #MeasureReport
* status = #active
* code = #belongs-to-study
* type = #reference
* expression = "MeasureReport.extension('https://miracum.github.io/recruit/fhir/StructureDefinition/belongs-to-study').value.ofType(Reference)"
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
* entry[3].resource = ObservationDerivedFromLibrary
* entry[3].request.method = #PUT
* entry[3].request.url = "SearchParameter/observation-derived-from-library"
* entry[4].resource = MeasureReportBelongsToStudy
* entry[4].request.method = #PUT
* entry[4].request.url = "SearchParameter/measurereport-belongs-to-study"
