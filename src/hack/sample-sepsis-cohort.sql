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
    ) AS met
FROM fhir.default.patient AS patient
