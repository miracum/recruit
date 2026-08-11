# Design: Per-Criterion Eligibility Status

!!! warning

    This is a design proposal, not yet implemented. It extends the
    [Trino SQL-based query module](index.md) and assumes that module as a
    prerequisite.

## Motivation

Today, `query-sql-on-fhir`'s `PollForStudies` evaluates exactly **one** SQL
query per study (`ResearchStudy.enrollment` -> `Group` with
`Group.code = trino-sql` -> the first `Group.characteristic` -> a `Library`
holding base64-encoded SQL). The query returns a flat list of `patient_id`s
that satisfy the *entire* criteria set, and those patients become
`ResearchSubject` entries on the study's screening `List`. There is no way to
tell, from the resulting FHIR data, *which* individual criteria a patient
does or doesn't satisfy - only the collapsed pass/fail outcome of the whole
query.

The goal of this design is to let `list-next` show, per patient, a
per-criterion breakdown with three states:

- **met** - the criterion is satisfied
- **not met** - the criterion is definitively not satisfied
- **unknown** - the data needed to evaluate the criterion doesn't exist

## Alternatives considered and rejected

- **`MeasureReport`** - built for quality-measure population counting, not
  per-patient per-criterion results. `population.count` is an integer
  (0/1 would be a hack for booleans), `population.code` is bound to a fixed
  quality-measure vocabulary with no slot for an arbitrary named criterion,
  and there's no native "unknown"/data-absent equivalent on `population`.
  Its nested `group[].population[]` shape is also awkward to flatten for
  SQL-on-FHIR ViewDefinitions compared to a flat `Observation` per fact. It
  could still be layered on top later as a per-patient "evaluation run"
  envelope referencing the `Observation`s below via `evaluatedResource`, but
  it doesn't replace them.
- **`Evidence` / `EvidenceVariable`** - built for evidence *synthesis*
  (systematic reviews, GRADE certainty ratings for published findings), not
  per-patient evaluation. `Evidence` has no `subject: Reference(Patient)` at
  all. `EvidenceVariable` can *define* a population by characteristics
  (similar to `Group.characteristic`) but has no element for recording a
  result against an individual patient, so `Observation` would still be
  needed downstream regardless. Its post-R5 characteristic model is also
  heavier and less broadly tooled (HAPI/Pathling) than `Group`/`Library`,
  which this module already depends on.
- **A dedicated `CodeSystem` per criterion** - implies upfront governance:
  every criterion, including one-off study-specific ones, would need a
  registered code, and "is this criterion shared with another study" becomes
  a coordination question. Rejected in favor of using the `Library` resource
  itself as the criterion's identity (see below) - sharing then falls out
  naturally from resource references rather than needing a controlled
  vocabulary.

## Criterion definition

Extend `Group.characteristic` from a single entry (today) to one entry per
criterion. `Group.code` stays `trino-sql` so the existing discovery query in
`PollForStudies` still finds it unchanged.

```json
{
  "resourceType": "Group",
  "id": "trial-042-eligibility",
  "type": "person",
  "actual": true,
  "code": {
    "coding": [{ "system": "https://recruit.miracum.org/fhir/CodeSystem/eligibility-criteria-types", "code": "trino-sql" }]
  },
  "characteristic": [
    {
      "code": { "text": "Age >= 18 years" },
      "valueReference": { "reference": "Library/trial-042-age-min-18" },
      "exclude": false
    },
    {
      "code": { "text": "HbA1c > 7.0%" },
      "valueReference": { "reference": "Library/trial-042-hba1c-elevated" },
      "exclude": false
    },
    {
      "code": { "text": "No prior chemotherapy" },
      "valueReference": { "reference": "Library/trial-042-no-prior-chemo" },
      "exclude": true
    }
  ]
}
```

Notes:

- `characteristic.code` is **text-only** (`CodeableConcept.text`, no
  `coding`) - valid FHIR, no controlled vocabulary required.
- The `Library` resource *is* the criterion's identity. A criterion that
  turns out to be reusable across studies is simply referenced by more than
  one `Group.characteristic`, from different `Group`s, pointing at the same
  `Library`:

  ```
  Group/trial-042  -> characteristic -> Library/common-age-min-18
  Group/trial-057  -> characteristic -> Library/common-age-min-18   (same resource)
  Group/trial-088  -> characteristic -> Library/trial-088-custom-ecog  (one-off)
  ```

  No dedup process is needed up front - start every criterion study-scoped
  and only point a second study's characteristic at an existing `Library`
  once it's clear the logic is genuinely identical. A future "catalog" of
  proven-reusable criteria is just `Library` resources with real canonical
  `url`s that studies opt into referencing.
- `characteristic.exclude` follows FHIR's own semantics: the `Library`'s SQL
  should compute the **raw, un-negated predicate** (e.g. "does the patient
  have a prior chemotherapy procedure"), and `exclude=true` says "reject
  patients for whom this is true." Negation is applied once, generically, in
  the merge step below - not duplicated by every SQL author.

## Library SQL contract

Every criterion `Library` must contain SQL returning exactly two columns,
covering the **full patient population** (so that "no matching data" reliably
produces `NULL`, not a missing row):

```sql
-- crit-age-min-18
SELECT p.id AS patient_id,
       CASE WHEN p.birth_date IS NULL THEN NULL
            ELSE date_diff('year', date(p.birth_date), current_date) >= 18
       END AS met
FROM fhir.default.patient p
```

```sql
-- crit-hba1c-elevated
SELECT p.id AS patient_id,
       CASE WHEN NOT EXISTS (
                SELECT 1 FROM fhir.default.observation o
                WHERE o.subject_reference = p.id AND o.code_coding_code = '4548-4' -- LOINC HbA1c
            ) THEN NULL
            ELSE (SELECT max(o.value_quantity_value) FROM fhir.default.observation o
                  WHERE o.subject_reference = p.id AND o.code_coding_code = '4548-4') > 7.0
       END AS met
FROM fhir.default.patient p
```

```sql
-- crit-no-prior-chemo (raw predicate; NOT pre-negated - exclude=true handles that)
SELECT p.id AS patient_id,
       EXISTS (SELECT 1 FROM fhir.default.procedure pr
               WHERE pr.subject_reference = p.id AND pr.code_coding_code = '367336001') AS has_chemo
FROM fhir.default.patient p
```

!!! note "Absence-of-evidence caveat"

    For exclusion criteria based on the *absence* of a record (like
    "no prior chemotherapy"), "no matching Procedure" is ambiguous: it could
    mean the patient genuinely never had chemotherapy, or that this site's
    data simply doesn't cover their full history. SQL can't tell these apart
    without an explicit completeness signal. This has to be decided
    per-criterion when it's authored - don't let every exclusion criterion
    silently default to "confirmed met" just because no rows were found.

## Merge logic

At the scale this needs to run at (~2 million patients per site), the merge
must not be done by pulling each criterion's full-population result set over
JDBC into the query module's JVM and folding them with in-memory maps - that
means N x 2M rows crossing the network and landing on the heap, when only a
much smaller candidate/unknown subset is ever actually needed downstream.
Instead, the merge is expressed as **one generated SQL query per study**,
built by wrapping each `Library`'s already-standalone SQL as a CTE:

```sql
WITH crit_0 AS ( <library-0-sql> ),   -- age-min-18, exclude=false
     crit_1 AS ( <library-1-sql> ),   -- hba1c-elevated, exclude=false
     crit_2 AS ( <library-2-sql> )    -- raw "has_chemo" predicate, exclude=true
SELECT
    p.id AS patient_id,
    c0.met AS crit_age_min_18,
    c1.met AS crit_hba1c_elevated,
    NOT c2.met AS crit_no_prior_chemo,        -- exclude negation applied here, once, generically
    (c0.met AND c1.met AND NOT c2.met) AS overall_met
FROM fhir.default.patient p
LEFT JOIN crit_0 c0 ON c0.patient_id = p.id
LEFT JOIN crit_1 c1 ON c1.patient_id = p.id
LEFT JOIN crit_2 c2 ON c2.patient_id = p.id
WHERE (c0.met AND c1.met AND NOT c2.met) IS DISTINCT FROM FALSE
```

Two things this relies on that are native SQL behavior, not custom logic:

- **`AND` over nullable booleans already implements Kleene three-valued
  logic** (`TRUE AND NULL = NULL`, `FALSE AND NULL = FALSE`,
  `TRUE AND TRUE = TRUE`) - one definite `false` dominates over any number of
  `unknown`s, and only turns into `unknown` overall when nothing is
  definitively false but something is missing.
- **`IS DISTINCT FROM FALSE`** filters to "candidate or unknown," dropping
  definite non-matches, and is pushed down so Trino/Delta never materializes
  or ships back the (likely large) portion of the population that fails
  outright.

This keeps each `Library`'s SQL independently authored, standalone, and
reusable (it still only needs to satisfy the two-column
`(patient_id, met)` contract to run alone) - the wrapper is mechanically
generated from `Group.characteristic`, not hand-written per study, so this
doesn't reintroduce fragile per-study SQL composition.

The query module's Java code shrinks to: loop over `Group.characteristic`,
template each `Library`'s SQL into a `crit_N` CTE plus one `LEFT JOIN` and
one (possibly negated) term in the final `AND`, execute **one** query, and
iterate the result - which now only contains patients who are true
candidates or genuinely undecidable, not the full 2 million.

## List / ResearchSubject membership

- `overall_met = true` or `overall_met = null` (unknown) -> patient gets a
  `ResearchSubject` + `List.entry`, same as today's
  `createScreeningListBundle`. Unknown patients are **not** dropped silently
  - "we don't have enough data to be sure" is exactly the kind of case a
  screening worklist should surface for human review, not hide.
- `overall_met = false` -> excluded from the `List` entirely, same as
  today's single-query behavior. Materializing an entry for every definite
  non-match doesn't scale and isn't what a screening worklist is for.

## Result representation

For every patient that ends up on the `List` (candidate or unknown), emit one
`Observation` per criterion - not for patients who were filtered out, since
nothing in the UI needs to explain a checklist for a patient nobody reviews.

```json
{
  "resourceType": "Observation",
  "status": "final",
  "identifier": [{ "system": "https://fhir.miracum.org/uc1/NamingSystem/eligibilityObservationId", "value": "<sha256 of patient+study+library>" }],
  "extension": [{ "url": "https://fhir.miracum.org/uc1/StructureDefinition/derivedFromLibrary", "valueReference": { "reference": "Library/trial-042-age-min-18" } }],
  "category": [{ "coding": [{ "system": "https://fhir.miracum.org/uc1/CodeSystem/observation-category", "code": "eligibility-assessment" }] }],
  "code": { "text": "Age >= 18 years" },
  "subject": { "reference": "Patient/123" },
  "focus": [{ "reference": "ResearchStudy/trial-042" }],
  "valueBoolean": true,
  "effectiveDateTime": "2026-08-10T09:00:00Z"
}
```

```json
{
  "resourceType": "Observation",
  "status": "final",
  "identifier": [{ "system": "https://fhir.miracum.org/uc1/NamingSystem/eligibilityObservationId", "value": "<sha256 of patient+study+library>" }],
  "extension": [{ "url": "https://fhir.miracum.org/uc1/StructureDefinition/derivedFromLibrary", "valueReference": { "reference": "Library/trial-042-hba1c-elevated" } }],
  "category": [{ "coding": [{ "system": "https://fhir.miracum.org/uc1/CodeSystem/observation-category", "code": "eligibility-assessment" }] }],
  "code": { "text": "HbA1c > 7.0%" },
  "subject": { "reference": "Patient/123" },
  "focus": [{ "reference": "ResearchStudy/trial-042" }],
  "dataAbsentReason": { "coding": [{ "system": "http://terminology.hl7.org/CodeSystem/data-absent-reason", "code": "unknown" }] },
  "effectiveDateTime": "2026-08-10T09:00:00Z"
}
```

```json
{
  "resourceType": "Observation",
  "status": "final",
  "identifier": [{ "system": "https://fhir.miracum.org/uc1/NamingSystem/eligibilityObservationId", "value": "<sha256 of patient+study+library>" }],
  "extension": [{ "url": "https://fhir.miracum.org/uc1/StructureDefinition/derivedFromLibrary", "valueReference": { "reference": "Library/trial-042-no-prior-chemo" } }],
  "category": [{ "coding": [{ "system": "https://fhir.miracum.org/uc1/CodeSystem/observation-category", "code": "eligibility-assessment" }] }],
  "code": { "text": "No prior chemotherapy" },
  "subject": { "reference": "Patient/123" },
  "focus": [{ "reference": "ResearchStudy/trial-042" }],
  "valueBoolean": false,
  "effectiveDateTime": "2026-08-10T09:00:00Z"
}
```

Notes:

- `valueBoolean` is always the **eligibility-facing** value (i.e. already
  negated per `exclude`, matching the `overall_met` computation) - a
  reviewer always reads `true = good` regardless of whether the underlying
  criterion was framed as inclusion or exclusion. This is the same
  `crit_N` column value the merge query already produced, just carried
  through unchanged.
- `category = eligibility-assessment` is a custom, non-standard code -
  deliberately, to keep these synthetic/derived Observations distinguishable
  from genuine clinical Observations elsewhere in the app's data.
- `focus` stays `ResearchStudy`-only, matching how it's already used on other
  resources in this app (context/scope, not the assessed entity). The
  criterion `Library` isn't a valid target for `Observation.derivedFrom`
  (HAPI rejects it: only
  `DocumentReference | ImagingStudy | ImagingSelection | QuestionnaireResponse
  | Observation` are allowed there), so it's referenced via the
  `derivedFromLibrary` extension instead - a plain `valueReference`, not
  `valueCanonical`, since `Library` is already referenced by relative
  reference elsewhere in this design (`Group.characteristic.valueReference`)
  and doesn't carry a canonical `.url`.
- `identifier` (a SHA-256 hash of `patient` + `study` + `library`) is the
  conditional-update key for this Observation, not `focus`/`derived-from`
  search parameters - the `derivedFromLibrary` extension's value isn't
  searchable without a custom `SearchParameter` registered on the FHIR
  server, but `identifier` is a standard, always-searchable parameter on
  every resource, so no server-side setup is needed. `code.text` is for
  display only; `identifier` (or a resolved `derivedFromLibrary`) is the
  computable join key for "all Observations for criterion X" (e.g. in a
  SQL-on-FHIR ViewDefinition or a `list-next` query).

## Bundle size at scale

Even with the `IS DISTINCT FROM FALSE` filter pushed down, the surviving
candidate/unknown set can still be large for an unselective study. Batch the
resulting FHIR transaction bundle (`ResearchSubject` + `List.entry` +
N x `Observation` per patient) into chunks of a few hundred/thousand entries
rather than one transaction for the whole study - most FHIR servers have
practical limits on single-transaction size regardless of how efficiently
the candidate set was computed.

## Implementation touchpoints

- `query-sql-on-fhir`'s `PollForStudies.java`:
  - Loop `group.getCharacteristic()` instead of `characteristicFirstRep()`.
  - Replace the single `jdbcTemplate.queryForList(contentString)` call with
    the generated wrapper query described above.
  - Extend `createScreeningListBundle` to also emit one `Observation` entry
    per `(patient, criterion)` for every patient added to the bundle, and to
    chunk large bundles into multiple transactions.
- `list-next`:
  - A read method (e.g. on `ResearchSubjectService`, or a new
    `EligibilityService`) querying
    `Observation?subject=Patient/{id}&focus=ResearchStudy/{studyId}&category=eligibility-assessment`,
    grouped/sorted by resolving each entry's `derivedFromLibrary` extension.
  - A UI surface for the per-criterion checklist - candidate slot: a new tab
    on `PatientDialog.razor`, or a header strip above the existing Clinical
    tab. Not yet decided.

## Open questions / follow-ups

- Exact chunk size for batched transaction bundles.
- Whether `list-next` needs a distinct visual treatment for "unknown"
  overall status on the patient list (today's `SystemDeterminedIneligible`
  flag only covers "system says no," not "system can't tell").
- Whether excluded (`overall_met = false`) patients need an audit trail at
  all, or whether "not on the list" is a sufficient record.
- Conventions for a shared `Library` "catalog" once enough criteria turn out
  to be reused across studies (canonical `url`s, versioning).
