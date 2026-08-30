SELECT
    patient.id AS patient_id,
    cast(NULL AS BOOLEAN) AS is_indeterminate,
    cast(NULL AS VARCHAR) AS result_note,
    date_diff('year', date(patient.birthdate), current_date) > 18 AS is_met
FROM fhir.default.patient AS patient
