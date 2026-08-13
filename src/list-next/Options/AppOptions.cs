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

/// <summary>
/// Config for the screening-list-change email notifier (Services/Notify). Deliberately a
/// separate section from NotificationOptions above, which is about in-app "is this
/// recommendation new/stalled" UI thresholds, not this background poller/mailer.
/// </summary>
public sealed class NotifyMailerOptions
{
    public const string SectionName = "NotifyMailer";

    /// <summary>FHIR search criteria used to find screening `List`s to poll, e.g. `List?code=...`.</summary>
    public required string ListSearchCriteria { get; set; }

    /// <summary>Used to link back to the screening list web app from an email. Must include `[list_id]`.</summary>
    public required string ScreeningListLinkTemplate { get; set; }

    /// <summary>The sender email address for notification mails.</summary>
    public required string From { get; set; }

    /// <summary>Subject line template, with `[study_acronym]` replaced at send time.</summary>
    public required string SubjectTemplate { get; set; }

    public required string SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 25;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }
}
