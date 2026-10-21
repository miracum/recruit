// A demo study whose eligibility criteria are evaluated by the query-sql-on-fhir module against the Synthea sample
// patients, loaded by the compose demo, the Helm chart's `sampleStudy` Job, and src/hack/compose.yaml via symlinks to
// the generated Bundle-recruit-sample-study-transaction.json.

// Every criterion Library carries its SQL once per supported dialect: query-sql-on-fhir runs the content matching its
// configured `query-sql-on-fhir.sql-dialect` - content[0] for Trino, content[1] for DuckDB.
//
// Attachment.data is base64-encoded, so each content's SQL is additionally carried as plain text in the SQL on FHIR
// sql-text extension to keep it readable here. Both must be kept in sync - CI checks that the data decodes to exactly
// the sql-text.
RuleSet: OpenRecruitIdentifier(type, value)
* identifier.system = "https://openrecruit.dev/fhir/identifiers/{type}"
* identifier.value = "{value}"

RuleSet: SqlQueryLibrary(name)
* status = #active
* name = "{name}"
* insert OpenRecruitIdentifier(trino-sql-criteria-library-id, {name})
* type = https://sql-on-fhir.org/ig/CodeSystem/LibraryTypesCodes#sql-query
* content[0].contentType = #"application/sql;dialect=trino"
* content[0].extension[0].url = "https://sql-on-fhir.org/ig/StructureDefinition/sql-text"
* content[1].contentType = #"application/sql;dialect=duckdb"
* content[1].extension[0].url = "https://sql-on-fhir.org/ig/StructureDefinition/sql-text"

Instance: SampleStudy
InstanceOf: ResearchStudy
Usage: #inline
* id = "98d96608-475f-4386-b7b5-6ddc1eb57e71"
* insert OpenRecruitIdentifier(trino-sql-criteria-researchstudy-id, bronchitis)
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
* insert OpenRecruitIdentifier(eligibility-group-id, bronchitis)
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
* characteristic[=].code.text = "No sepsis diagnosis within 3 months of the bronchitis diagnosis"
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
        ) AS is_met
    FROM fhir.default.patient AS patient
    """
* content[0].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBFWElTVFMgKAogICAgICAgIFNFTEVDVCAxCiAgICAgICAgRlJPTQogICAgICAgICAgICBmaGlyLmRlZmF1bHQuY29uZGl0aW9uLAogICAgICAgICAgICBVTk5FU1QoY29uZGl0aW9uLmNvZGUuY29kaW5nKSBBUyBjb25kaXRpb25fY29kaW5nCiAgICAgICAgV0hFUkUKICAgICAgICAgICAgY29uZGl0aW9uLnN1YmplY3QucmVmZXJlbmNlID0gQ09OQ0FUKCdQYXRpZW50LycsIHBhdGllbnQuaWQpCiAgICAgICAgICAgIEFORCBjb25kaXRpb25fY29kaW5nLnN5c3RlbSA9ICdodHRwOi8vc25vbWVkLmluZm8vc2N0JwogICAgICAgICAgICBBTkQgY29uZGl0aW9uX2NvZGluZy5jb2RlIElOICgnMTA1MDkwMDInKQogICAgKSBBUyBpc19tZXQKRlJPTSBmaGlyLmRlZmF1bHQucGF0aWVudCBBUyBwYXRpZW50"
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
        ) AS is_met
    FROM fhir.default.patient AS patient
    """
* content[1].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBFWElTVFMgKAogICAgICAgIFNFTEVDVCAxCiAgICAgICAgRlJPTQogICAgICAgICAgICBmaGlyLmRlZmF1bHQuY29uZGl0aW9uLAogICAgICAgICAgICBVTk5FU1QoY29uZGl0aW9uLmNvZGUuY29kaW5nKSBBUyB1KGNvbmRpdGlvbl9jb2RpbmcpCiAgICAgICAgV0hFUkUKICAgICAgICAgICAgY29uZGl0aW9uLnN1YmplY3QucmVmZXJlbmNlID0gQ09OQ0FUKCdQYXRpZW50LycsIHBhdGllbnQuaWQpCiAgICAgICAgICAgIEFORCBjb25kaXRpb25fY29kaW5nLnN5c3RlbSA9ICdodHRwOi8vc25vbWVkLmluZm8vc2N0JwogICAgICAgICAgICBBTkQgY29uZGl0aW9uX2NvZGluZy5jb2RlIElOICgnMTA1MDkwMDInKQogICAgKSBBUyBpc19tZXQKRlJPTSBmaGlyLmRlZmF1bHQucGF0aWVudCBBUyBwYXRpZW50"

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
        date_diff('year', date(patient.birthdate), current_date) > 18 AS is_met
    FROM fhir.default.patient AS patient
    """
* content[0].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBkYXRlX2RpZmYoJ3llYXInLCBkYXRlKHBhdGllbnQuYmlydGhkYXRlKSwgY3VycmVudF9kYXRlKSA+IDE4IEFTIGlzX21ldApGUk9NIGZoaXIuZGVmYXVsdC5wYXRpZW50IEFTIHBhdGllbnQ="
* content[1].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        date_sub('year', CAST(patient.birthdate AS DATE), current_date) > 18 AS is_met
    FROM fhir.default.patient AS patient
    """
* content[1].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBkYXRlX3N1YigneWVhcicsIENBU1QocGF0aWVudC5iaXJ0aGRhdGUgQVMgREFURSksIGN1cnJlbnRfZGF0ZSkgPiAxOCBBUyBpc19tZXQKRlJPTSBmaGlyLmRlZmF1bHQucGF0aWVudCBBUyBwYXRpZW50"

Instance: SampleStudySepsisLibrary
InstanceOf: Library
Usage: #inline
* id = "9be54194-a4d9-4835-bf9e-21314e4f046c"
* insert SqlQueryLibrary(sepsis)
* title = "Sepsis within 3 months of bronchitis"
* description = "Sepsis diagnosis (SNOMED CT code 91302008) with an onset within 3 months before or after a bronchitis diagnosis (SNOMED CT code 10509002), falling back to the recorded date where no onset is documented."
* content[0].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        EXISTS (
            SELECT 1
            FROM
                fhir.default.condition AS sepsis,
                UNNEST(sepsis.code.coding) AS sepsis_coding,
                fhir.default.condition AS bronchitis,
                UNNEST(bronchitis.code.coding) AS bronchitis_coding
            WHERE
                sepsis.subject.reference = CONCAT('Patient/', patient.id)
                AND sepsis_coding.system = 'http://snomed.info/sct'
                AND sepsis_coding.code IN ('91302008')
                AND bronchitis.subject.reference = sepsis.subject.reference
                AND bronchitis_coding.system = 'http://snomed.info/sct'
                AND bronchitis_coding.code IN ('10509002')
                AND from_iso8601_timestamp(COALESCE(sepsis.onsetdatetime, sepsis.recordeddate))
                    BETWEEN from_iso8601_timestamp(COALESCE(bronchitis.onsetdatetime, bronchitis.recordeddate)) - INTERVAL '3' MONTH
                        AND from_iso8601_timestamp(COALESCE(bronchitis.onsetdatetime, bronchitis.recordeddate)) + INTERVAL '3' MONTH
        ) AS is_met
    FROM fhir.default.patient AS patient
    """
* content[0].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBFWElTVFMgKAogICAgICAgIFNFTEVDVCAxCiAgICAgICAgRlJPTQogICAgICAgICAgICBmaGlyLmRlZmF1bHQuY29uZGl0aW9uIEFTIHNlcHNpcywKICAgICAgICAgICAgVU5ORVNUKHNlcHNpcy5jb2RlLmNvZGluZykgQVMgc2Vwc2lzX2NvZGluZywKICAgICAgICAgICAgZmhpci5kZWZhdWx0LmNvbmRpdGlvbiBBUyBicm9uY2hpdGlzLAogICAgICAgICAgICBVTk5FU1QoYnJvbmNoaXRpcy5jb2RlLmNvZGluZykgQVMgYnJvbmNoaXRpc19jb2RpbmcKICAgICAgICBXSEVSRQogICAgICAgICAgICBzZXBzaXMuc3ViamVjdC5yZWZlcmVuY2UgPSBDT05DQVQoJ1BhdGllbnQvJywgcGF0aWVudC5pZCkKICAgICAgICAgICAgQU5EIHNlcHNpc19jb2Rpbmcuc3lzdGVtID0gJ2h0dHA6Ly9zbm9tZWQuaW5mby9zY3QnCiAgICAgICAgICAgIEFORCBzZXBzaXNfY29kaW5nLmNvZGUgSU4gKCc5MTMwMjAwOCcpCiAgICAgICAgICAgIEFORCBicm9uY2hpdGlzLnN1YmplY3QucmVmZXJlbmNlID0gc2Vwc2lzLnN1YmplY3QucmVmZXJlbmNlCiAgICAgICAgICAgIEFORCBicm9uY2hpdGlzX2NvZGluZy5zeXN0ZW0gPSAnaHR0cDovL3Nub21lZC5pbmZvL3NjdCcKICAgICAgICAgICAgQU5EIGJyb25jaGl0aXNfY29kaW5nLmNvZGUgSU4gKCcxMDUwOTAwMicpCiAgICAgICAgICAgIEFORCBmcm9tX2lzbzg2MDFfdGltZXN0YW1wKENPQUxFU0NFKHNlcHNpcy5vbnNldGRhdGV0aW1lLCBzZXBzaXMucmVjb3JkZWRkYXRlKSkKICAgICAgICAgICAgICAgIEJFVFdFRU4gZnJvbV9pc284NjAxX3RpbWVzdGFtcChDT0FMRVNDRShicm9uY2hpdGlzLm9uc2V0ZGF0ZXRpbWUsIGJyb25jaGl0aXMucmVjb3JkZWRkYXRlKSkgLSBJTlRFUlZBTCAnMycgTU9OVEgKICAgICAgICAgICAgICAgICAgICBBTkQgZnJvbV9pc284NjAxX3RpbWVzdGFtcChDT0FMRVNDRShicm9uY2hpdGlzLm9uc2V0ZGF0ZXRpbWUsIGJyb25jaGl0aXMucmVjb3JkZWRkYXRlKSkgKyBJTlRFUlZBTCAnMycgTU9OVEgKICAgICkgQVMgaXNfbWV0CkZST00gZmhpci5kZWZhdWx0LnBhdGllbnQgQVMgcGF0aWVudA=="
* content[1].extension[0].valueString = """
    SELECT
        patient.id AS patient_id,
        EXISTS (
            SELECT 1
            FROM
                fhir.default.condition AS sepsis,
                UNNEST(sepsis.code.coding) AS u(sepsis_coding),
                fhir.default.condition AS bronchitis,
                UNNEST(bronchitis.code.coding) AS v(bronchitis_coding)
            WHERE
                sepsis.subject.reference = CONCAT('Patient/', patient.id)
                AND sepsis_coding.system = 'http://snomed.info/sct'
                AND sepsis_coding.code IN ('91302008')
                AND bronchitis.subject.reference = sepsis.subject.reference
                AND bronchitis_coding.system = 'http://snomed.info/sct'
                AND bronchitis_coding.code IN ('10509002')
                AND CAST(COALESCE(sepsis.onsetdatetime, sepsis.recordeddate) AS TIMESTAMPTZ)
                    BETWEEN CAST(COALESCE(bronchitis.onsetdatetime, bronchitis.recordeddate) AS TIMESTAMPTZ) - INTERVAL 3 MONTH
                        AND CAST(COALESCE(bronchitis.onsetdatetime, bronchitis.recordeddate) AS TIMESTAMPTZ) + INTERVAL 3 MONTH
        ) AS is_met
    FROM fhir.default.patient AS patient
    """
* content[1].data = "U0VMRUNUCiAgICBwYXRpZW50LmlkIEFTIHBhdGllbnRfaWQsCiAgICBFWElTVFMgKAogICAgICAgIFNFTEVDVCAxCiAgICAgICAgRlJPTQogICAgICAgICAgICBmaGlyLmRlZmF1bHQuY29uZGl0aW9uIEFTIHNlcHNpcywKICAgICAgICAgICAgVU5ORVNUKHNlcHNpcy5jb2RlLmNvZGluZykgQVMgdShzZXBzaXNfY29kaW5nKSwKICAgICAgICAgICAgZmhpci5kZWZhdWx0LmNvbmRpdGlvbiBBUyBicm9uY2hpdGlzLAogICAgICAgICAgICBVTk5FU1QoYnJvbmNoaXRpcy5jb2RlLmNvZGluZykgQVMgdihicm9uY2hpdGlzX2NvZGluZykKICAgICAgICBXSEVSRQogICAgICAgICAgICBzZXBzaXMuc3ViamVjdC5yZWZlcmVuY2UgPSBDT05DQVQoJ1BhdGllbnQvJywgcGF0aWVudC5pZCkKICAgICAgICAgICAgQU5EIHNlcHNpc19jb2Rpbmcuc3lzdGVtID0gJ2h0dHA6Ly9zbm9tZWQuaW5mby9zY3QnCiAgICAgICAgICAgIEFORCBzZXBzaXNfY29kaW5nLmNvZGUgSU4gKCc5MTMwMjAwOCcpCiAgICAgICAgICAgIEFORCBicm9uY2hpdGlzLnN1YmplY3QucmVmZXJlbmNlID0gc2Vwc2lzLnN1YmplY3QucmVmZXJlbmNlCiAgICAgICAgICAgIEFORCBicm9uY2hpdGlzX2NvZGluZy5zeXN0ZW0gPSAnaHR0cDovL3Nub21lZC5pbmZvL3NjdCcKICAgICAgICAgICAgQU5EIGJyb25jaGl0aXNfY29kaW5nLmNvZGUgSU4gKCcxMDUwOTAwMicpCiAgICAgICAgICAgIEFORCBDQVNUKENPQUxFU0NFKHNlcHNpcy5vbnNldGRhdGV0aW1lLCBzZXBzaXMucmVjb3JkZWRkYXRlKSBBUyBUSU1FU1RBTVBUWikKICAgICAgICAgICAgICAgIEJFVFdFRU4gQ0FTVChDT0FMRVNDRShicm9uY2hpdGlzLm9uc2V0ZGF0ZXRpbWUsIGJyb25jaGl0aXMucmVjb3JkZWRkYXRlKSBBUyBUSU1FU1RBTVBUWikgLSBJTlRFUlZBTCAzIE1PTlRICiAgICAgICAgICAgICAgICAgICAgQU5EIENBU1QoQ09BTEVTQ0UoYnJvbmNoaXRpcy5vbnNldGRhdGV0aW1lLCBicm9uY2hpdGlzLnJlY29yZGVkZGF0ZSkgQVMgVElNRVNUQU1QVFopICsgSU5URVJWQUwgMyBNT05USAogICAgKSBBUyBpc19tZXQKRlJPTSBmaGlyLmRlZmF1bHQucGF0aWVudCBBUyBwYXRpZW50"

Instance: RecruitSampleStudyTransaction
InstanceOf: Bundle
Usage: #example
Description: "A demo ResearchStudy with three SQL-encoded eligibility criteria, each in both the Trino and the DuckDB dialect: a bronchitis diagnosis, an age over 18, and no sepsis diagnosis within 3 months of the bronchitis diagnosis."
* id = "recruit-sample-study-transaction"
* type = #transaction
* insert PutEntry(SampleStudy, ResearchStudy/98d96608-475f-4386-b7b5-6ddc1eb57e71)
* insert PutEntry(SampleStudyEligibilityGroup, Group/be24c1a6-b052-4360-871b-0a1b7b6bdfb4)
* insert PutEntry(SampleStudyBronchitisLibrary, Library/bb4e037a-0a43-4fee-befc-b9ed598c3e64)
* insert PutEntry(SampleStudyAgeOver18Library, Library/12b09320-2426-43e9-930d-64f151d39e68)
* insert PutEntry(SampleStudySepsisLibrary, Library/9be54194-a4d9-4835-bf9e-21314e4f046c)
