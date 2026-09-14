namespace RecruIT.List.Data.Entities;

/// <summary>
/// One detected "a patient was newly recommended" fact, written once by NotificationDetectorService
/// and never mutated. Keyed by the trial's business identifier ("system|value", see
/// TrialIdentifier), not the FHIR List's logical id - that's server-local and can change (e.g. if a
/// study's screening list is ever recreated), so it's resolved fresh via
/// ScreeningListService.ResolveListForTrialAsync whenever a link is actually needed (sender,
/// feed), rather than cached here. PatientDisplayName is still denormalized (a snapshot, same as
/// ScreeningNote.AuthorDisplayName) since it has no such correctness requirement and re-resolving
/// it would cost a FHIR read per event for no benefit. DedupeKey
/// ("{trialIdentifier}:{listVersionId}:{patientReference}") makes the insert idempotent if the
/// detector's poll tick overlaps across replicas.
/// </summary>
public sealed class NotificationEvent
{
    public Guid Id { get; set; }

    public required string TrialIdentifier { get; set; }

    public required string PatientReference { get; set; }

    public required string PatientDisplayName { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public required string DedupeKey { get; set; }
}
