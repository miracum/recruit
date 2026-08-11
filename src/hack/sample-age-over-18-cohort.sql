SELECT
    patient.id AS patient_id,
    date_diff('year', date(patient.birthdate), current_date) > 18 AS met
FROM fhir.default.patient AS patient
