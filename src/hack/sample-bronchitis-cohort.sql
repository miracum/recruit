SELECT DISTINCT patient.id AS patient_id
FROM fhir.default.patient AS patient
LEFT JOIN
    fhir.default.condition
    ON condition.subject.reference = CONCAT('Patient/', patient.id),
    UNNEST(condition.code.coding) AS condition_coding
WHERE
    condition_coding.system = 'http://snomed.info/sct'
    AND condition_coding.code IN ('10509002')
