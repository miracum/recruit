using BlazorBlueprint.Components;
using Duende.AccessTokenManagement.OpenIdConnect;
using list.Components;
using list.Options;
using list.Services.Access;
using list.Services.Auth;
using list.Services.Fhir;
using list.Services.Localization;
using list.Services.Navigation;
using list.Services.Notifications;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization();
builder.Services.AddBlazorBlueprintComponents();
builder.Services.AddScoped<IBbLocalizer, AppBbLocalizer>();

var supportedCultures = new[] { "en", "de" };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(supportedCultures[0])
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

builder.Services.Configure<FhirOptions>(builder.Configuration.GetSection(FhirOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));

var fhirBaseUrl = builder.Configuration.GetValue<string>("Fhir:BaseUrl")
    ?? throw new InvalidOperationException("Fhir:BaseUrl must be configured.");
var authDisabled = builder.Configuration.GetValue<bool>("Auth:Disabled");

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole(TrialAccessService.AdminRole));
});

if (authDisabled)
{
    builder.Services.AddAuthentication(DevBypassAuthenticationHandler.SchemeName)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevBypassAuthenticationHandler>(
            DevBypassAuthenticationHandler.SchemeName, _ => { });
    builder.Services.AddAuthorization();
    builder.Services.AddHttpClient(FhirClientFactory.HttpClientName, client => client.BaseAddress = new Uri(fhirBaseUrl));
}
else
{
    var oidcSection = builder.Configuration.GetSection("Oidc");
    var roleClaimType = oidcSection["RoleClaimType"] is { Length: > 0 } rc ? rc : "role";

    builder.Services.AddTransient<CookieEvents>();
    builder.Services.AddTransient<OidcEvents>();

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options => options.EventsType = typeof(CookieEvents))
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
    builder.Services.AddOpenIdConnectAccessTokenManagement()
        .AddBlazorServerAccessTokenManagement<ServerSideTokenStore>();
    builder.Services.AddUserAccessTokenHttpClient(
        FhirClientFactory.HttpClientName,
        configureClient: client => client.BaseAddress = new Uri(fhirBaseUrl));
}

builder.Services.AddSingleton<TrialAccessService>();
builder.Services.AddSingleton<FhirClientFactory>();
builder.Services.AddScoped<ScreeningListService>();
builder.Services.AddScoped<ResearchSubjectService>();
builder.Services.AddScoped<PatientRecordService>();
builder.Services.AddScoped<NotificationDismissalService>();
builder.Services.AddScoped<BreadcrumbState>();

var app = builder.Build();

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

if (!authDisabled)
{
    var authGroup = app.MapGroup("/authentication");
    authGroup.MapGet("/login", (string? returnUrl) =>
        Results.Challenge(
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
            [OpenIdConnectDefaults.AuthenticationScheme]));

    // GET is needed because Blazor Server's NavigationManager.NavigateTo(forceLoad: true) - the
    // only way to trigger a real HTTP request (and thus an auth-cookie-clearing response) from an
    // interactive circuit - always issues a GET.
    authGroup.MapMethods("/logout", ["GET", "POST"], (string? returnUrl) =>
        Results.SignOut(
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
}

app.MapGet("/culture/set", (string culture, string? redirectUri, HttpContext ctx) =>
{
    var resolvedCulture = supportedCultures.Contains(culture) ? culture : supportedCultures[0];

    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(resolvedCulture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

    return Results.LocalRedirect(string.IsNullOrEmpty(redirectUri) ? "/" : redirectUri);
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
