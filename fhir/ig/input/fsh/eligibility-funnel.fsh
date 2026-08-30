CodeSystem: EligibilityFunnelPopulationType
Id: eligibility-funnel-population-type
Title: "recruIT eligibility funnel population type"
Description: "The two population kinds making up a study's attrition-funnel MeasureReport.group.population: the total screened population, and the population remaining after cumulatively applying one more criterion."
* #total-population "Total population"
* #after-criterion "After criterion"

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
* uniqueId.value = "https://miracum.github.io/recruit/fhir/identifiers/eligibility-funnel-measure-id"
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
* uniqueId.value = "https://miracum.github.io/recruit/fhir/identifiers/eligibility-funnel-report-id"
* uniqueId.preferred = true
