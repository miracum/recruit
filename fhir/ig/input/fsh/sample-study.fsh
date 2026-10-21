// A demo study whose eligibility criteria are evaluated by the query-sql-on-fhir module against the Synthea sample
// patients, loaded by the compose demo, the Helm chart's `sampleStudy` Job, and src/hack/compose.yaml via symlinks to
// the generated Bundle-recruit-sample-study-transaction.json.

// Every criterion Library carries its SQL once per supported dialect: query-sql-on-fhir runs the content matching its
// configured `query-sql-on-fhir.sql-dialect` - content[0] for Trino, content[1] for DuckDB.
//
// Attachment.data is base64-encoded, so each content's SQL is additionally carried as plain text in the SQL on FHIR
// sql-text extension to keep it readable here. Both must be kept in sync - CI checks that the data decodes to exactly
// the sql-text.
RuleSet: SqlQueryLibrary(name)
* status = #active
* name = "{name}"
* identifier.system = "https://openrecruit.dev/fhir/identifiers/trino-sql-criteria-library-id"
* identifier.value = "{name}"
* type = https://sql-on-fhir.org/ig/CodeSystem/LibraryTypesCodes#sql-query
* content[0].contentType = #"application/sql;dialect=trino"
* content[0].extension[0].url = "https://sql-on-fhir.org/ig/StructureDefinition/sql-text"
* content[1].contentType = #"application/sql;dialect=duckdb"
* content[1].extension[0].url = "https://sql-on-fhir.org/ig/StructureDefinition/sql-text"

Instance: SampleStudy
InstanceOf: ResearchStudy
Usage: #inline
* id = "98d96608-475f-4386-b7b5-6ddc1eb57e71"
* identifier.system = "https://openrecruit.dev/fhir/identifiers/trino-sql-criteria-researchstudy-id"
* identifier.value = "bronchitis"
* extension[0].url = "https://fhir.miracum.org/uc1/StructureDefinition/studyAcronym"
* extension[0].valueString = "DEMO-BRONCHITIS"
* extension[1].url = "https://www.medizininformatik-initiative.de/fhir/modul-studie/StructureDefinition/mii-ex-studie-akronym"
* extension[1].valueString = "DEMO-BRONCHITIS"
* status = #active
* title = "Bronchitis Demo Study"
* enrollment = Reference(SampleStudyEligibilityGroup)

Instance: SampleStudyEligibilityGroup
InstanceOf: Group
Usage: #inline
* id = "be24c1a6-b052-4360-871b-0a1b7b6bdfb4"
* identifier.system = "https://openrecruit.dev/fhir/identifiers/eligibility-group-id"
* identifier.value = "bronchitis"
* actual = false
* type = #person
* active = true
* code = https://openrecruit.dev/fhir/CodeSystem/eligibility-criteria-types#trino-sql
* code.text = "Trino SQL-encoded eligibility criteria"
* characteristic[0].valueReference = Reference(SampleStudyBronchitisLibrary)
* characteristic[=].code.text = "Bronchitis diagnosis"
* characteristic[=].exclude = false
* characteristic[+].valueReference = Reference(SampleStudyAgeOver18Library)
* characteristic[=].code.text = "Age > 18 years"
* characteristic[=].exclude = false
* characteristic[+].valueReference = Reference(SampleStudySepsisLibrary)
* characteristic[=].code.text = "No prior sepsis"
* characteristic[=].exclude = true

Instance: SampleStudyBronchitisLibrary
InstanceOf: Library
Usage: #inline
* id = "bb4e037a-0a43-4fee-befc-b9ed598c3e64"
* insert SqlQueryLibrary(bronchitis)
* title = "Bronchitis diagnosis"
* description = "Active bronchitis diagnosis (SNOMED CT code 10509002)."
* content[0].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        EXISTS (
            SELECT 1
            FROM
                fhir.default.condition,
                UNNEST(condition.code.coding) AS condition_coding
            WHERE
                condition.subject.reference = CONCAT('Patient/', patient.id)
                AND condition_coding.system = 'http://snomed.info/sct'
                AND condition_coding.code IN ('10509002')
        ) AS is_met,
        CAST(NULL AS BOOLEAN) AS is_indeterminate,
        CAST(NULL AS VARCHAR) AS result_note
    FROM fhir.default.patient AS patient
    """
* content[0].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBFWElTVFMgKAogICAgICAgIFNFTEVDVCAxCiAgICAgICAgRlJPTQogICAgICAgICAgICBmaGlyLmRlZmF1bHQuY29uZGl0aW9uLAogICAgICAgICAgICBVTk5FU1QoY29uZGl0aW9uLmNvZGUuY29kaW5nKSBBUyBjb25kaXRpb25fY29kaW5nCiAgICAgICAgV0hFUkUKICAgICAgICAgICAgY29uZGl0aW9uLnN1YmplY3QucmVmZXJlbmNlID0gQ09OQ0FUKCdQYXRpZW50LycsIHBhdGllbnQuaWQpCiAgICAgICAgICAgIEFORCBjb25kaXRpb25fY29kaW5nLnN5c3RlbSA9ICdodHRwOi8vc25vbWVkLmluZm8vc2N0JwogICAgICAgICAgICBBTkQgY29uZGl0aW9uX2NvZGluZy5jb2RlIElOICgnMTA1MDkwMDInKQogICAgKSBBUyBpc19tZXQsCiAgICBDQVNUKE5VTEwgQVMgQk9PTEVBTikgQVMgaXNfaW5kZXRlcm1pbmF0ZSwKICAgIENBU1QoTlVMTCBBUyBWQVJDSEFSKSBBUyByZXN1bHRfbm90ZQpGUk9NIGZoaXIuZGVmYXVsdC5wYXRpZW50IEFTIHBhdGllbnQ="
* content[1].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        EXISTS (
            SELECT 1
            FROM
                fhir.default.condition,
                UNNEST(condition.code.coding) AS u(condition_coding)
            WHERE
                condition.subject.reference = CONCAT('Patient/', patient.id)
                AND condition_coding.system = 'http://snomed.info/sct'
                AND condition_coding.code IN ('10509002')
        ) AS is_met,
        CAST(NULL AS BOOLEAN) AS is_indeterminate,
        CAST(NULL AS VARCHAR) AS result_note
    FROM fhir.default.patient AS patient
    """
* content[1].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBFWElTVFMgKAogICAgICAgIFNFTEVDVCAxCiAgICAgICAgRlJPTQogICAgICAgICAgICBmaGlyLmRlZmF1bHQuY29uZGl0aW9uLAogICAgICAgICAgICBVTk5FU1QoY29uZGl0aW9uLmNvZGUuY29kaW5nKSBBUyB1KGNvbmRpdGlvbl9jb2RpbmcpCiAgICAgICAgV0hFUkUKICAgICAgICAgICAgY29uZGl0aW9uLnN1YmplY3QucmVmZXJlbmNlID0gQ09OQ0FUKCdQYXRpZW50LycsIHBhdGllbnQuaWQpCiAgICAgICAgICAgIEFORCBjb25kaXRpb25fY29kaW5nLnN5c3RlbSA9ICdodHRwOi8vc25vbWVkLmluZm8vc2N0JwogICAgICAgICAgICBBTkQgY29uZGl0aW9uX2NvZGluZy5jb2RlIElOICgnMTA1MDkwMDInKQogICAgKSBBUyBpc19tZXQsCiAgICBDQVNUKE5VTEwgQVMgQk9PTEVBTikgQVMgaXNfaW5kZXRlcm1pbmF0ZSwKICAgIENBU1QoTlVMTCBBUyBWQVJDSEFSKSBBUyByZXN1bHRfbm90ZQpGUk9NIGZoaXIuZGVmYXVsdC5wYXRpZW50IEFTIHBhdGllbnQ="

Instance: SampleStudyAgeOver18Library
InstanceOf: Library
Usage: #inline
* id = "12b09320-2426-43e9-930d-64f151d39e68"
* insert SqlQueryLibrary(age-over-18)
* title = "Age over 18"
* description = "Patient is over 18 years old."
* content[0].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        cast(NULL AS BOOLEAN) AS is_indeterminate,
        cast(NULL AS VARCHAR) AS result_note,
        date_diff('year', date(patient.birthdate), current_date) > 18 AS is_met
    FROM fhir.default.patient AS patient
    """
* content[0].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBjYXN0KE5VTEwgQVMgQk9PTEVBTikgQVMgaXNfaW5kZXRlcm1pbmF0ZSwKICAgIGNhc3QoTlVMTCBBUyBWQVJDSEFSKSBBUyByZXN1bHRfbm90ZSwKICAgIGRhdGVfZGlmZigneWVhcicsIGRhdGUocGF0aWVudC5iaXJ0aGRhdGUpLCBjdXJyZW50X2RhdGUpID4gMTggQVMgaXNfbWV0CkZST00gZmhpci5kZWZhdWx0LnBhdGllbnQgQVMgcGF0aWVudA=="
* content[1].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        cast(NULL AS BOOLEAN) AS is_indeterminate,
        cast(NULL AS VARCHAR) AS result_note,
        date_sub('year', CAST(patient.birthdate AS DATE), current_date) > 18 AS is_met
    FROM fhir.default.patient AS patient
    """
* content[1].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBjYXN0KE5VTEwgQVMgQk9PTEVBTikgQVMgaXNfaW5kZXRlcm1pbmF0ZSwKICAgIGNhc3QoTlVMTCBBUyBWQVJDSEFSKSBBUyByZXN1bHRfbm90ZSwKICAgIGRhdGVfc3ViKCd5ZWFyJywgQ0FTVChwYXRpZW50LmJpcnRoZGF0ZSBBUyBEQVRFKSwgY3VycmVudF9kYXRlKSA+IDE4IEFTIGlzX21ldApGUk9NIGZoaXIuZGVmYXVsdC5wYXRpZW50IEFTIHBhdGllbnQ="

Instance: SampleStudySepsisLibrary
InstanceOf: Library
Usage: #inline
* id = "9be54194-a4d9-4835-bf9e-21314e4f046c"
* insert SqlQueryLibrary(sepsis)
* title = "Sepsis condition"
* description = "Active sepsis condition (SNOMED CT code 91302008) documented at any time."
* content[0].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        EXISTS (
            SELECT 1
            FROM
                fhir.default.condition,
                UNNEST(condition.code.coding) AS condition_coding
            WHERE
                condition.subject.reference = CONCAT('Patient/', patient.id)
                AND condition_coding.system = 'http://snomed.info/sct'
                AND condition_coding.code IN ('91302008')
        ) AS is_met,
        CAST(NULL AS BOOLEAN) AS is_indeterminate,
        CAST(NULL AS VARCHAR) AS result_note
    FROM fhir.default.patient AS patient
    """
* content[0].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBFWElTVFMgKAogICAgICAgIFNFTEVDVCAxCiAgICAgICAgRlJPTQogICAgICAgICAgICBmaGlyLmRlZmF1bHQuY29uZGl0aW9uLAogICAgICAgICAgICBVTk5FU1QoY29uZGl0aW9uLmNvZGUuY29kaW5nKSBBUyBjb25kaXRpb25fY29kaW5nCiAgICAgICAgV0hFUkUKICAgICAgICAgICAgY29uZGl0aW9uLnN1YmplY3QucmVmZXJlbmNlID0gQ09OQ0FUKCdQYXRpZW50LycsIHBhdGllbnQuaWQpCiAgICAgICAgICAgIEFORCBjb25kaXRpb25fY29kaW5nLnN5c3RlbSA9ICdodHRwOi8vc25vbWVkLmluZm8vc2N0JwogICAgICAgICAgICBBTkQgY29uZGl0aW9uX2NvZGluZy5jb2RlIElOICgnOTEzMDIwMDgnKQogICAgKSBBUyBpc19tZXQsCiAgICBDQVNUKE5VTEwgQVMgQk9PTEVBTikgQVMgaXNfaW5kZXRlcm1pbmF0ZSwKICAgIENBU1QoTlVMTCBBUyBWQVJDSEFSKSBBUyByZXN1bHRfbm90ZQpGUk9NIGZoaXIuZGVmYXVsdC5wYXRpZW50IEFTIHBhdGllbnQ="
* content[1].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        EXISTS (
            SELECT 1
            FROM
                fhir.default.condition,
                UNNEST(condition.code.coding) AS u(condition_coding)
            WHERE
                condition.subject.reference = CONCAT('Patient/', patient.id)
                AND condition_coding.system = 'http://snomed.info/sct'
                AND condition_coding.code IN ('91302008')
        ) AS is_met,
        CAST(NULL AS BOOLEAN) AS is_indeterminate,
        CAST(NULL AS VARCHAR) AS result_note
    FROM fhir.default.patient AS patient
    """
* content[1].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBFWElTVFMgKAogICAgICAgIFNFTEVDVCAxCiAgICAgICAgRlJPTQogICAgICAgICAgICBmaGlyLmRlZmF1bHQuY29uZGl0aW9uLAogICAgICAgICAgICBVTk5FU1QoY29uZGl0aW9uLmNvZGUuY29kaW5nKSBBUyB1KGNvbmRpdGlvbl9jb2RpbmcpCiAgICAgICAgV0hFUkUKICAgICAgICAgICAgY29uZGl0aW9uLnN1YmplY3QucmVmZXJlbmNlID0gQ09OQ0FUKCdQYXRpZW50LycsIHBhdGllbnQuaWQpCiAgICAgICAgICAgIEFORCBjb25kaXRpb25fY29kaW5nLnN5c3RlbSA9ICdodHRwOi8vc25vbWVkLmluZm8vc2N0JwogICAgICAgICAgICBBTkQgY29uZGl0aW9uX2NvZGluZy5jb2RlIElOICgnOTEzMDIwMDgnKQogICAgKSBBUyBpc19tZXQsCiAgICBDQVNUKE5VTEwgQVMgQk9PTEVBTikgQVMgaXNfaW5kZXRlcm1pbmF0ZSwKICAgIENBU1QoTlVMTCBBUyBWQVJDSEFSKSBBUyByZXN1bHRfbm90ZQpGUk9NIGZoaXIuZGVmYXVsdC5wYXRpZW50IEFTIHBhdGllbnQ="

Instance: RecruitSampleStudyTransaction
InstanceOf: Bundle
Usage: #example
Description: "A demo ResearchStudy with three SQL-encoded eligibility criteria, each in both the Trino and the DuckDB dialect: a bronchitis diagnosis, an age over 18, and no prior sepsis."
* id = "recruit-sample-study-transaction"
* type = #transaction
* insert PutEntry(SampleStudy, ResearchStudy/98d96608-475f-4386-b7b5-6ddc1eb57e71)
* insert PutEntry(SampleStudyEligibilityGroup, Group/be24c1a6-b052-4360-871b-0a1b7b6bdfb4)
* insert PutEntry(SampleStudyBronchitisLibrary, Library/bb4e037a-0a43-4fee-befc-b9ed598c3e64)
* insert PutEntry(SampleStudyAgeOver18Library, Library/12b09320-2426-43e9-930d-64f151d39e68)
* insert PutEntry(SampleStudySepsisLibrary, Library/9be54194-a4d9-4835-bf9e-21314e4f046c)
