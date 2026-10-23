using System.Globalization;
using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace RecruIT.List.E2ETests;

/// <summary>
/// Boots a real RecruIT.List instance in-process (via WebApplicationFactory, with a real Kestrel
/// listener so Blazor Server's SignalR circuit works - the default TestServer doesn't carry
/// WebSockets) against disposable Testcontainers Postgres and HAPI FHIR servers, for the
/// Playwright tests to drive with an actual browser. The FHIR server is seeded with a screening
/// list (see FhirSeed), so tests can go through real trial pages. Auth:Enabled=false swaps in
/// DevBypassAuthenticationHandler (an admin with an email address), so no IdP is needed, and
/// Notifications:Testing:Enabled turns on the Notification Test page's re-recommend action.
///
/// Set <see cref="BaseUrlEnvVar"/> to point the same tests at an already-running instance instead
/// - e.g. a post-deploy smoke test against a container/Kubernetes deployment in CI - in which case
/// this fixture starts neither the containers nor the app itself. That instance needs at least
/// one trial with patients on its screening list, and Notifications:Testing:Enabled.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    /// <summary>
    /// Shared by every test class, so they run against one app and one set of containers rather
    /// than each starting its own.
    /// </summary>
    public const string Name = "app";
}

public sealed class AppFixture : IAsyncLifetime
{
    private const string BaseUrlEnvVar = "E2E_BASE_URL";

    /// <summary>
    /// The instance's metrics endpoint, for <see cref="BaseUrlEnvVar"/> - it's on a separate port
    /// (METRICS_PORT), which usually isn't reachable from wherever the app itself is. Optional.
    /// </summary>
    private const string MetricsUrlEnvVar = "E2E_METRICS_URL";

    private const int FhirPort = 8080;

    private WebApplicationFactory<Program>? _factory;
    private PostgreSqlContainer? _postgres;
    private IContainer? _fhir;

    public string BaseUrl { get; private set; } = "";

    /// <summary>The app's Prometheus scrape endpoint, or null if it isn't known (see <see cref="MetricsUrlEnvVar"/>).</summary>
    public string? MetricsUrl { get; private set; }

    public async ValueTask InitializeAsync()
    {
        // Ensures the Chromium build Playwright needs is present without depending on the
        // pwsh-only `playwright.ps1 install` script, so `dotnet test`/`dotnet run` work as-is.
        Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (Environment.GetEnvironmentVariable(BaseUrlEnvVar) is { Length: > 0 } externalUrl)
        {
            BaseUrl = externalUrl.TrimEnd('/');
            MetricsUrl = Environment.GetEnvironmentVariable(MetricsUrlEnvVar)
                is { Length: > 0 } metricsUrl
                ? metricsUrl
                : null;
            return;
        }

        _postgres = new PostgreSqlBuilder(
            "docker.io/library/postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
        ).Build();
        // Same image as src/hack/compose.yaml. Its in-memory H2 database is plenty for a test run.
        _fhir = new ContainerBuilder(
            "docker.io/hapiproject/hapi:v8.12.0-2@sha256:0ff86aac4c7bd13a1ee226444a70263bb46ed1c28ef543c30a44f865b80ca04e"
        )
            .WithPortBinding(FhirPort, assignRandomHostPort: true)
            // Otherwise HAPI hands back an identical search's results for a minute, so e.g. a List
            // written after a search for it stays invisible to that same search.
            .WithEnvironment("HAPI_FHIR_REUSE_CACHED_SEARCH_RESULTS_MILLIS", "0")
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request =>
                        request.ForPort(FhirPort).ForPath("/fhir/metadata")
                    )
            )
            .Build();
        // HAPI takes far longer to come up than Postgres, so start both at once.
        await Task.WhenAll(_postgres.StartAsync(), _fhir.StartAsync());

        var fhirBaseUrl =
            $"http://{_fhir.Hostname}:{_fhir.GetMappedPublicPort(FhirPort).ToString(CultureInfo.InvariantCulture)}/fhir";
        await FhirSeed.SeedAsync(fhirBaseUrl, TimeSpan.FromSeconds(60));

        var httpPort = GetFreeTcpPort();
        var metricsPort = GetFreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{httpPort.ToString(CultureInfo.InvariantCulture)}";
        MetricsUrl =
            $"http://127.0.0.1:{metricsPort.ToString(CultureInfo.InvariantCulture)}/metrics";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // Program.cs configures Kestrel itself from these two settings (see its comment on
            // why that - rather than ASPNETCORE_URLS/UseUrls - is the source of truth here).
            builder.UseSetting("HTTP_PORT", httpPort.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("METRICS_PORT", metricsPort.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("ConnectionStrings:AppDb", _postgres.GetConnectionString());
            builder.UseSetting("Auth:Enabled", "false");
            builder.UseSetting("FhirServer:BaseUrl", fhirBaseUrl);
            builder.UseSetting("Notifications:Testing:Enabled", "true");
        });
        // The default TestServer is in-memory only and can't carry Blazor Server's SignalR
        // circuit - UseKestrel() (WithWebHostBuilder's clone doesn't inherit it from the base
        // factory, so it must be called on this instance) makes the factory bind a real,
        // browser-reachable listener instead. Must be called before first touching
        // Services/Server/CreateClient(), which is what actually starts the host.
        _factory.UseKestrel();
        _ = _factory.Services;

        await WaitUntilReadyAsync(TimeSpan.FromSeconds(60));
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        if (_fhir is not null)
        {
            await _fhir.DisposeAsync();
        }
    }

    private async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync($"{BaseUrl}/readyz");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException($"App did not become ready at '{BaseUrl}' within {timeout}.");
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
