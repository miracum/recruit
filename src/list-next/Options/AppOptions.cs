namespace list.Options;

public sealed class FhirOptions
{
    public const string SectionName = "Fhir";

    public required string BaseUrl { get; set; }
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Dev-only escape hatch (mirrors list-old's KEYCLOAK_DISABLED): skips OIDC entirely and
    /// treats every visitor as an authenticated admin. Never enable this outside local development.
    /// </summary>
    public bool Disabled { get; set; }
}

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public int NewSuggestionWindowDays { get; set; } = 7;

    /// <summary>
    /// A pending patient (candidate/screening/eligible) whose ResearchSubject hasn't been touched
    /// in this many days counts as a "stalled lead" - nobody has acted on it in a while.
    /// </summary>
    public int StalledLeadWindowDays { get; set; } = 14;

    public int ScanIntervalSeconds { get; set; } = 60;
}
