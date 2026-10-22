namespace RecruIT.List.Options;

public sealed class FhirServerOptions
{
    public const string SectionName = "FhirServer";

    public required string BaseUrl { get; set; }
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Whether visitors sign in via OIDC (configured in the nested Oidc section, see
    /// OidcSectionName). When false (the default), OIDC is skipped entirely and every visitor is
    /// treated as an authenticated admin - only ever leave it off for local development and demos.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>The OIDC role value that grants global admin privileges.</summary>
    public string AdminRole { get; set; } = "admin";

    /// <summary>
    /// The OpenIdConnectOptions bound as-is (Authority, ClientId, ClientSecret, ...), plus this
    /// app's own RoleClaimType, GroupClaimType and GroupRoleMappings - see Program.cs and OidcEvents.
    /// </summary>
    public const string OidcSectionName = $"{SectionName}:Oidc";
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
/// Config for the screening-list-change email notifier (Services/Notify), nested under the
/// Notifications section but bound separately from NotificationOptions above, which is about the
/// in-app "is this recommendation new" UI threshold and the scan interval, not the mail delivery.
/// </summary>
public sealed class EmailNotificationOptions
{
    public const string SectionName = $"{NotificationOptions.SectionName}:Email";

    /// <summary>
    /// The app's public root URL as users reach it, e.g. "https://recruit.example.com/" - every
    /// link in a notification email is built from this by the app itself (see PublicUrl).
    /// </summary>
    public required string PublicBaseUrl { get; set; }

    /// <summary>
    /// Resolves an app-relative path against PublicBaseUrl, keeping any path base it has (e.g.
    /// "https://host/recruit" + "notifications" is "https://host/recruit/notifications") whether or
    /// not PublicBaseUrl ends in a slash - plain Uri resolution would drop "recruit" without one.
    /// </summary>
    public string PublicUrl(string relativePath)
    {
        var baseUri = new Uri(
            PublicBaseUrl.EndsWith('/') ? PublicBaseUrl : PublicBaseUrl + "/",
            UriKind.Absolute
        );
        return new Uri(baseUri, relativePath.TrimStart('/')).ToString();
    }

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
