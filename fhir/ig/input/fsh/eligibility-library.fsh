// An R4 port of the SQL on FHIR SQLQuery profile (https://sql-on-fhir.org/ig/StructureDefinition/SQLQuery), which is
// only published for R5.
Profile: SQLQueryLibrary
Parent: Library
Id: sql-query-library
Title: "SQL Query Library"
Description: "A Library holding a SQL-encoded eligibility criterion, one content per supported SQL dialect. Mirrors the SQL on FHIR SQLQuery profile."
* obeys sql-must-be-sql-expressions
* type = https://sql-on-fhir.org/ig/CodeSystem/LibraryTypesCodes#sql-query
* content 1..*
* content.contentType 1..1
* content.data 1..1

Invariant: sql-must-be-sql-expressions
Description: "The content of the Library must be SQL expressions."
Expression: "content.all(contentType.startsWith('application/sql'))"
Severity: #error

Instance: EligibilityLibraryIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a criterion expressed in a Library resource"
* id = "eligibility-library-id"
* name = "EligibilityLibraryIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-14"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/eligibility-library-id"
* uniqueId.preferred = true

Instance: SqlCriteriaLibraryIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a SQLQueryLibrary criterion authored as FSH, such as the demo study's criteria."
* id = "sql-criteria-library-id"
* name = "SqlCriteriaLibraryIdentifier"
* status = #active
* kind = #identifier
* date = "2026-10-21"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/sql-criteria-library-id"
* uniqueId.preferred = true

Instance: SupersetSyncedEligibilityLibraryIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a criterion expressed in a Library resource when synced from a Superset instance."
* id = "superset-synced-eligibility-library-id"
* name = "SupersetSyncedEligibilityLibraryIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-14"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/superset-synced-eligibility-library-id"
* uniqueId.preferred = true

Instance: UICreatedEligibilityLibraryIdentifierSystem
InstanceOf: NamingSystem
Usage: #definition
Description: "The identifier system for a criterion expressed in a Library resource created via the UI."
* id = "ui-created-eligibility-library-id"
* name = "UICreatedEligibilityLibraryIdentifier"
* status = #active
* kind = #identifier
* date = "2026-08-14"
* uniqueId.type = #uri
* uniqueId.value = "https://openrecruit.dev/fhir/identifiers/ui-created-eligibility-library-id"
* uniqueId.preferred = true

Instance: SQLQueryLibraryExample
InstanceOf: SQLQueryLibrary
Description: "An example of a SQL-encoded eligibility criterion that every patient meets."
* id = "sql-query-library-example"
* status = #active
* name = "all-patients"
* title = "All patients"
* type = https://sql-on-fhir.org/ig/CodeSystem/LibraryTypesCodes#sql-query
* content[0].contentType = #"application/sql;dialect=trino"
* content[0].data = "U0VMRUNUIHBhdGllbnQuaWQgQVMgcGF0aWVudF9pZCwgVFJVRSBBUyBpc19tZXQgRlJPTSBmaGlyLmRlZmF1bHQucGF0aWVudCBBUyBwYXRpZW50"
* content[0].extension[0].url = "https://sql-on-fhir.org/ig/StructureDefinition/sql-text"
* content[0].extension[0].valueString = "SELECT patient.id AS patient_id, TRUE AS is_met FROM fhir.default.patient AS patient"
