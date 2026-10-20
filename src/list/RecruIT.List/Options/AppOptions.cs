namespace RecruIT.List.Options;

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

    /// <summary>The OIDC role value that grants global admin privileges.</summary>
    public string AdminRole { get; set; } = "admin";
}

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public int NewSuggestionWindowDays { get; set; } = 7;

    public int ScanIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Off by default - exporting spans nobody collects is just per-request overhead for nothing, so
/// this is opt-in per environment rather than always-on. The exporter destination itself
/// (OTEL_EXPORTER_OTLP_ENDPOINT, _PROTOCOL, etc.) is deliberately not modeled here: the
/// OpenTelemetry SDK already reads those standard env vars/config keys on its own, the same way
/// the other OTEL_* instrumented services in src/hack/compose.yaml are configured.
/// </summary>
public sealed class TracingOptions
{
    public const string SectionName = "Tracing";

    public bool Enabled { get; set; }
}

/// <summary>
/// Config for the screening-list-change email notifier (Services/Notify). Deliberately a
/// separate section from NotificationOptions above, which is about the in-app "is this
/// recommendation new" UI threshold, not this background poller/mailer.
/// </summary>
public sealed class NotifyMailerOptions
{
    public const string SectionName = "NotifyMailer";

    /// <summary>
    /// Used to link back to the screening list web app from an email - a composite format string
    /// (see string.Format) with a single `{0}` placeholder for the list id.
    /// </summary>
    public required string ScreeningListLinkTemplate { get; set; }

    /// <summary>The sender email address for notification mails.</summary>
    public required string From { get; set; }

    /// <summary>
    /// Subject line template - a composite format string (see string.Format) with a single `{0}`
    /// placeholder for the study acronym.
    /// </summary>
    public required string SubjectTemplate { get; set; }

    public required string SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 25;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }
}
