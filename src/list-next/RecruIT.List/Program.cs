using BlazorBlueprint.Components;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RecruIT.List.Components;
using RecruIT.List.Data;
using RecruIT.List.Models;
using RecruIT.List.Options;
using RecruIT.List.Services;
using RecruIT.List.Services.Access;
using RecruIT.List.Services.Auth;
using RecruIT.List.Services.Fhir;
using RecruIT.List.Services.Localization;
using RecruIT.List.Services.Navigation;
using RecruIT.List.Services.Notifications;
using RecruIT.List.Services.Notify;

var builder = WebApplication.CreateBuilder(args);

// A dedicated metrics port (separate from the app's HTTP_PORT) keeps /metrics off the
// internet-facing listener - only the in-cluster ServiceMonitor needs to reach it. Configuring
// both endpoints in code (rather than relying on ASPNETCORE_HTTP_PORTS/ASPNETCORE_URLS) is
// required here: once Kestrel has any code- or config-based endpoint, it stops honoring the
// URLs-based env vars for the rest, so both ports must be declared together.
var httpPort = builder.Configuration.GetValue<int?>("HTTP_PORT") ?? 8080;
var metricsPort = builder.Configuration.GetValue<int?>("METRICS_PORT") ?? 8081;
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(httpPort);
    kestrel.ListenAnyIP(metricsPort);
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddLocalization();
builder.Services.AddBlazorBlueprintComponents();
builder.Services.AddScoped<IBbLocalizer, AppBbLocalizer>();

var supportedCultures = new[] { "en", "de" };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options
        .SetDefaultCulture(supportedCultures[0])
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

builder.Services.Configure<FhirOptions>(builder.Configuration.GetSection(FhirOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName)
);
builder.Services.Configure<NotifyMailerOptions>(
    builder.Configuration.GetSection(NotifyMailerOptions.SectionName)
);
builder.Services.Configure<TracingOptions>(
    builder.Configuration.GetSection(TracingOptions.SectionName)
);

var fhirBaseUrl =
    builder.Configuration.GetValue<string>("Fhir:BaseUrl")
    ?? throw new InvalidOperationException("Fhir:BaseUrl must be configured.");
var authDisabled = builder.Configuration.GetValue<bool>("Auth:Disabled");

// Read once at startup rather than threading IOptions<AuthOptions> through every call site.
if (builder.Configuration["Auth:AdminRole"] is { Length: > 0 } adminRole)
{
    TrialAccessService.AdminRole = adminRole;
}

var appDbConnectionStringBuilder = new NpgsqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("AppDb")
        ?? throw new InvalidOperationException("ConnectionStrings:AppDb must be configured.")
);

var appDbConnectionString = appDbConnectionStringBuilder.ConnectionString;

// Liveness (/livez) intentionally checks nothing but that the process can still handle a request -
// it shouldn't flap just because a downstream dependency is briefly unavailable. Readiness (/readyz)
// only runs checks tagged "ready", currently just the DB, since that's the one dependency this app
// can't do anything useful without.
builder
    .Services.AddHealthChecks()
    .AddNpgSql(appDbConnectionString, name: "postgres", tags: ["ready"]);

var otelBuilder = builder
    .Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
        metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddPrometheusExporter()
    );

var tracingEnabled = builder.Configuration.GetValue<bool>($"{TracingOptions.SectionName}:Enabled");

// Opt-in rather than always-on: exporting spans to a collector nobody deployed is just
// per-request overhead for nothing. AddOtlpExporter() with no config reads the standard
// OTEL_EXPORTER_OTLP_ENDPOINT/_PROTOCOL/OTEL_SERVICE_NAME env vars (or appsettings keys of the
// same name) itself - the same convention the other OTEL_*-instrumented services in
// src/hack/compose.yaml already use, so no separate endpoint setting is needed here.
if (tracingEnabled)
{
    otelBuilder.WithTracing(tracing =>
        tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter()
    );
}

// AddDbContextFactory (not AddDbContext) is required in Blazor Server: DI scopes there are
// per-circuit (roughly per user session), not per-request, so a directly-injected DbContext would
// live far longer than intended. Services instead resolve IDbContextFactory<AppDbContext> and
// create a short-lived context per operation.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options
        .UseNpgsql(
            appDbConnectionString,
            npgsql =>
                npgsql
                    .MapEnum<TrialPermissionLevel>("trial_permission_level")
                    .MapEnum<NotificationChannel>("notification_channel")
        )
        .UseSnakeCaseNamingConvention()
);

// Blazor Server keeps one process per replica but many circuits/cookies across replicas - the
// Data Protection key ring that encrypts auth cookies/antiforgery tokens needs to be shared so a
// request landing on a different replica than the one that issued the cookie can still decrypt it
// (relevant even with ingress session affinity, e.g. right after a scaling event). This must not
// be scoped to Authorization:IsEnabled - app.UseAntiforgery() below is unconditional (Blazor
// Server's own circuit handshake relies on it regardless of whether OIDC auth is on), so even an
// auth-disabled deployment needs a shared key ring across replicas, not just a single-process
// fallback. Persisted to the same Postgres database as AppDb rather than a separate Redis instance
// - sticky sessions themselves are an ingress-level concern, documented in the README, not
// implemented here.
builder.Services.AddDbContext<DataProtectionKeyContext>(options =>
    options.UseNpgsql(appDbConnectionString).UseSnakeCaseNamingConvention()
);
builder.Services.AddDataProtection().PersistKeysToDbContext<DataProtectionKeyContext>();

// Hangfire coordinates the notification poller across all list-next replicas via this same
// Postgres database - see Services/Notify and the notify-next plan for why (a plain
// UPDATE ... WHERE last_seen_version_id = @original concurrency-token check on PollCursor is the
// actual dedup mechanism; Hangfire just gives each replica a clustered recurring trigger and a
// distributed, retried delivery queue on top of that).
builder.Services.AddHangfire(config =>
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(appDbConnectionString, _ => { })
        )
);
builder.Services.AddHangfireServer();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole(TrialAccessService.AdminRole));
});

if (authDisabled)
{
    builder
        .Services.AddAuthentication(DevBypassAuthenticationHandler.SchemeName)
        .AddScheme<
            Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
            DevBypassAuthenticationHandler
        >(DevBypassAuthenticationHandler.SchemeName, _ => { });
    builder.Services.AddAuthorization();
}
else
{
    var oidcSection = builder.Configuration.GetSection("Oidc");
    var roleClaimType = oidcSection["RoleClaimType"] is { Length: > 0 } rc ? rc : "role";

    builder.Services.AddTransient<OidcEvents>();

    builder
        .Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            oidcSection.Bind(options);
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.ResponseMode = OpenIdConnectResponseMode.Query;
            options.SaveTokens = true;
            options.MapInboundClaims = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            // Only relax to plain-HTTP metadata/issuer for local development (e.g. a local Keycloak
            // without TLS) - production authorities must always be HTTPS.
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            // Opt-in only: ASP.NET Core defaults to using Pushed Authorization Requests whenever the
            // IdP advertises the endpoint, which added an extra failure mode against this app's
            // Keycloak setup for no real benefit at this app's size.
            options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.Scope.Add("offline_access");
            options.TokenValidationParameters.RoleClaimType = roleClaimType;
            options.TokenValidationParameters.NameClaimType = "preferred_username";
            options.EventsType = typeof(OidcEvents);
        });

    builder.Services.AddAuthorization();
}

// TODO: the FHIR server call currently carries no credentials at all - same as Auth:Disabled
// mode. This app used to forward the signed-in user's OIDC access token (AddUserAccessTokenHttpClient),
// but per-user FHIR authorization isn't needed right now; a static/service-account credential
// (the app calling FHIR as itself, not as any particular user) should replace this later.
builder.Services.AddHttpClient(
    FhirClientFactory.HttpClientName,
    client => client.BaseAddress = new Uri(fhirBaseUrl)
);

builder.Services.AddScoped<TrialAccessService>();
builder.Services.AddSingleton<FhirClientFactory>();
builder.Services.AddScoped<ScreeningListService>();
builder.Services.AddScoped<ResearchSubjectService>();
builder.Services.AddScoped<ScreeningNoteService>();
builder.Services.AddScoped<PatientRecordService>();
builder.Services.AddScoped<EligibilityCriteriaService>();
builder.Services.AddScoped<NotificationDismissalService>();
builder.Services.AddScoped<BreadcrumbState>();

builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
builder.Services.AddScoped<ScreeningListPollService>();
builder.Services.AddScoped<NotificationDeliveryService>();

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost,
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<
        IDbContextFactory<AppDbContext>
    >();
    await using var db = await dbContextFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    var dataProtectionKeyContext =
        scope.ServiceProvider.GetRequiredService<DataProtectionKeyContext>();
    await dataProtectionKeyContext.Database.MigrateAsync();
}

// Every list-next replica runs a Hangfire server (AddHangfireServer above); they self-coordinate
// via the shared Postgres storage, so registering the recurring job on every replica's startup is
// safe and idempotent (Hangfire dedupes by the job id, "poll-screening-lists"). Uses the DI-based
// IRecurringJobManager rather than the static RecurringJob API, since the static API relies on
// JobStorage.Current having already been initialized by AddHangfire's lazy configuration - which
// hasn't necessarily happened yet this early.
await using (var scope = app.Services.CreateAsyncScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<ScreeningListPollService>(
        "poll-screening-lists",
        s => s.PollAllTrialsAsync(CancellationToken.None),
        Cron.Minutely()
    );
}

// Keeps /metrics off the app's public-facing port: ASP.NET Core routing doesn't care which Kestrel
// listener accepted a connection, so without this, ingress traffic (which forwards "/" to the same
// port list-next's main listener serves) could reach the Prometheus exporter too - defeating the
// point of a separate metrics port. Checks the actual accepted connection's local port rather than
// RequireHost's Host-header matching, since the Host header can desync from the real port behind
// port-forwarding/NAT. /livez and /readyz don't get the same treatment: kubelet always probes the
// "http" port by name, and a plain Healthy/Unhealthy response isn't sensitive either way.
app.Use(
    async (context, next) =>
    {
        if (context.Request.Path == "/metrics" && context.Connection.LocalPort != metricsPort)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next(context);
    }
);

app.UseRequestLocalization();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard(
    "/hangfire",
    new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter(authDisabled)],
    }
);

app.MapHealthChecks("/livez", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks(
    "/readyz",
    new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }
);

app.MapPrometheusScrapingEndpoint();

if (!authDisabled)
{
    var authGroup = app.MapGroup("/authentication");
    authGroup.MapGet(
        "/login",
        (string? returnUrl) =>
            Results.Challenge(
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    RedirectUri = returnUrl ?? "/",
                },
                [OpenIdConnectDefaults.AuthenticationScheme]
            )
    );

    // GET is needed because Blazor Server's NavigationManager.NavigateTo(forceLoad: true) - the
    // only way to trigger a real HTTP request (and thus an auth-cookie-clearing response) from an
    // interactive circuit - always issues a GET.
    authGroup.MapMethods(
        "/logout",
        ["GET", "POST"],
        (string? returnUrl) =>
            Results.SignOut(
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    RedirectUri = returnUrl ?? "/logged-out",
                },
                [
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    OpenIdConnectDefaults.AuthenticationScheme,
                ]
            )
    );
}

app.MapGet(
    "/culture/set",
    (string culture, string? redirectUri, HttpContext ctx) =>
    {
        var resolvedCulture = supportedCultures.Contains(culture) ? culture : supportedCultures[0];

        ctx.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(resolvedCulture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
        );

        return Results.LocalRedirect(string.IsNullOrEmpty(redirectUri) ? "/" : redirectUri);
    }
);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
