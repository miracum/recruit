using list.Models;

namespace list.Data.Entities;

/// <summary>
/// One user's permission level on one trial. Keyed by the trial's ResearchStudy business
/// identifier (TrialIdentifierSystem/Value), not its FHIR id or acronym - see TrialIdentifier.
/// </summary>
public sealed class TrialAccessGrant
{
    public Guid Id { get; set; }

    public required string TrialIdentifierSystem { get; set; }

    public required string TrialIdentifierValue { get; set; }

    /// <summary>OIDC "sub". Null until the invited user's first login backfills it (see OidcEvents).</summary>
    public string? SubjectId { get; set; }

    /// <summary>Invite key and display; kept in sync from claims on every login.</summary>
    public required string Email { get; set; }

    public string? DisplayName { get; set; }

    public required TrialPermissionLevel Level { get; set; }

    /// <summary>Sub or email of whoever created/last changed this grant, for basic traceability.</summary>
    public required string GrantedBy { get; set; }

    public DateTimeOffset GrantedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
