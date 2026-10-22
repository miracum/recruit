CodeSystem: EligibilityFunnelPopulationType
Id: eligibility-funnel-population-type
Title: "recruIT eligibility funnel population type"
Description: "The two population kinds making up a study's attrition-funnel MeasureReport.group.population: the total screened population, and the population remaining after cumulatively applying one more criterion."
* #total-population "Total population"
* #after-criterion "After criterion"

ValueSet: EligibilityFunnelPopulationTypes
Id: eligibility-funnel-population-types
Title: "recruIT eligibility funnel population types"
Description: "The population kinds a study's attrition-funnel MeasureReport.group.population may be coded as."
* include codes from system EligibilityFunnelPopulationType

Invariant: eligibility-funnel-measure-url
Description: "The Measure's url is the recruIT Measure base url followed by an id derived from its business identifier."
Expression: "url.startsWith('https://openrecruit.dev/fhir/Measure/')"
Severity: #error

Profile: EligibilityFunnelMeasure
Parent: Measure
Id: eligibility-funnel-measure
Title: "recruIT eligibility funnel Measure"
Description: "The Measure a study's attrition-funnel MeasureReport is reported against. Nothing is evaluated as a quality measure - it exists so MeasureReport.measure has a real canonical to resolve, and lists the criterion Libraries the funnel steps were derived from."
* obeys eligibility-funnel-measure-url
* url 1..1
* identifier 1..1
* identifier.system = "https://openrecruit.dev/fhir/identifiers/eligibility-funnel-measure-id"
* identifier.value 1..1
* title 1..1

Profile: EligibilityFunnelReport
Parent: MeasureReport
Id: eligibility-funnel-report
Title: "recruIT eligibility funnel MeasureReport"
Description: "A study's eligibility attrition funnel: the total screened population, followed by one population per eligibility criterion holding the number of patients remaining after cumulatively applying it, in Group.characteristic order."
* extension contains BelongsToStudy named belongsToStudy 1..1
* identifier 1..1
* identifier.system = "https://openrecruit.dev/fhir/identifiers/eligibility-funnel-report-id"
* identifier.value 1..1
* status = #complete
* type = #summary
* measure only Canonical(EligibilityFunnelMeasure)
* group 1..1
* group.population 1..*
* group.population.code 1..1
* group.population.code from EligibilityFunnelPopulationTypes (required)
* group.population.count 1..1
* group.population.extension contains DerivedFromLibrary named derivedFromLibrary 0..1

Instance: EligibilityFunnelMeasureExample
InstanceOf: EligibilityFunnelMeasure
Description: "An example of a study's attrition-funnel Measure."
* id = "eligibility-funnel-measure-example"
* url = "https://openrecruit.dev/fhir/Measure/eligibility-funnel-measure-example"
* identifier.value = "example"
* status = #active
* title = "EXAMPLE eligibility attrition funnel"
* library = Canonical(SQLQueryLibraryExample)

Instance: EligibilityFunnelReportExample
InstanceOf: EligibilityFunnelReport
Description: "An example of a study's attrition-funnel MeasureReport with a single criterion step."
* id = "eligibility-funnel-report-example"
* extension[belongsToStudy].valueReference = Reference(RecruitResearchStudyExample)
* identifier.value = "example"
* measure = Canonical(EligibilityFunnelMeasureExample)
* date = "2026-10-22T12:00:00Z"
* period.start = "2026-10-22T12:00:00Z"
* period.end = "2026-10-22T12:00:00Z"
* group.code.text = "Eligibility attrition funnel"
* group.population[0].code = EligibilityFunnelPopulationType#total-population
* group.population[=].count = 120
* group.population[+].code = EligibilityFunnelPopulationType#after-criterion
* group.population[=].code.text = "All patients"
* group.population[=].count = 120
* group.population[=].extension[derivedFromLibrary].valueReference = Reference(SQLQueryLibraryExample)

Instance: EligibilityFunnelMeasureIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a study's attrition-funnel Measure's business identifier - used as the conditional-update key, keyed on the same value as the study's own identifier."
* id = "eligibility-funnel-measure-id"
* name = "EligibilityFunnelMeasureIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-27"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/eligibility-funnel-measure-id"
* uniqueId.preferred = true

Instance: EligibilityFunnelReportIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a study's attrition-funnel MeasureReport's business identifier - used as the conditional-update key, keyed on the same value as the study's own identifier."
* id = "eligibility-funnel-report-id"
* name = "EligibilityFunnelReportIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-27"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/eligibility-funnel-report-id"
* uniqueId.preferred = true
