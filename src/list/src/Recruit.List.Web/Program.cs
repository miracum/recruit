using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using Recruit.List.Web.Auth;
using Recruit.List.Web.Components;
using Recruit.List.Web.Fhir;

namespace Recruit.List.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // matches the container port the existing Helm chart/k8s probes and docker-compose
        // files already expect (see charts/recruit/templates/list/list-deployment.yaml).
        var port = builder.Configuration["PORT"] ?? "8080";
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        var keycloakDisabled = builder.Configuration.GetValue("KEYCLOAK_DISABLED", false);

        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddMudServices();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCascadingAuthenticationState();

        builder.Services.AddScoped<TokenProvider>();

        var fhirUrl = builder.Configuration["FHIR_URL"] ?? "http://localhost:8082/fhir";
        builder.Services.AddHttpClient<IFhirService, FhirService>(client =>
        {
            client.BaseAddress = new Uri(fhirUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        if (keycloakDisabled)
        {
            builder
                .Services.AddAuthentication(NoAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>(
                    NoAuthHandler.SchemeName,
                    null
                );
        }
        else
        {
            var authUrl = builder.Configuration["KEYCLOAK_AUTH_URL"] ?? "http://localhost:8083";
            var realm = builder.Configuration["KEYCLOAK_REALM"] ?? "MIRACUM";
            var clientId = builder.Configuration["KEYCLOAK_CLIENT_ID"] ?? "uc1-screeninglist";
            var clientSecret = builder.Configuration["KEYCLOAK_CLIENT_SECRET"];
            // internal cluster traffic to Keycloak is frequently plain HTTP; only require HTTPS metadata
            // when explicitly asked to.
            var requireHttpsMetadata = builder.Configuration.GetValue(
                "KEYCLOAK_REQUIRE_HTTPS_METADATA",
                false
            );

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
                    options.Authority = $"{authUrl.TrimEnd('/')}/realms/{realm}";
                    options.ClientId = clientId;
                    options.ClientSecret = clientSecret;
                    options.RequireHttpsMetadata = requireHttpsMetadata;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.TokenValidationParameters.NameClaimType = "preferred_username";
                    options.TokenValidationParameters.RoleClaimType = "roles";
                    options.SignedOutRedirectUri = "/";
                });
        }

        builder.Services.AddAuthorization();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapGet(
                "/api/health/{probe}",
                (string probe) => Results.Ok(new { probe, status = "ok" })
            )
            .AllowAnonymous();

        if (!keycloakDisabled)
        {
            app.MapGet(
                    "/Account/Login",
                    (string? returnUrl) =>
                        Results.Challenge(
                            new AuthenticationProperties
                            {
                                RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl,
                            },
                            [OpenIdConnectDefaults.AuthenticationScheme]
                        )
                )
                .AllowAnonymous();

            app.MapGet(
                "/Account/Logout",
                (HttpContext httpContext) =>
                    Results.SignOut(
                        new AuthenticationProperties { RedirectUri = "/" },
                        [
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            OpenIdConnectDefaults.AuthenticationScheme,
                        ]
                    )
            );
        }

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        // Older deployments and notification email templates may still link to routes from the
        // previous per-study list (e.g. /recommendations/{listId}); send those to the root page
        // instead of a bare 404.
        app.MapFallback(context =>
        {
            context.Response.Redirect("/");
            return Task.CompletedTask;
        });

        app.Run();
    }
}
