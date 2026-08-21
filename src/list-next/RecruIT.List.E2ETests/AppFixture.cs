using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace RecruIT.List.E2ETests;

/// <summary>
/// Boots a real RecruIT.List instance in-process (via WebApplicationFactory, with a real Kestrel
/// listener so Blazor Server's SignalR circuit works - the default TestServer doesn't carry
/// WebSockets) against a disposable Testcontainers Postgres, for the Playwright tests to drive
/// with an actual browser. Auth:Disabled swaps in DevBypassAuthenticationHandler so no IdP is
/// needed, and Fhir:BaseUrl points at a closed port: the main page renders its own error state if
/// FHIR is unreachable (see Dashboard.razor), so this is enough to prove the app shell itself
/// comes up.
///
/// Set <see cref="BaseUrlEnvVar"/> to point the same tests at an already-running instance instead
/// - e.g. a post-deploy smoke test against a container/Kubernetes deployment in CI - in which case
/// this fixture starts neither Postgres nor the app itself.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    private const string BaseUrlEnvVar = "E2E_BASE_URL";

    private WebApplicationFactory<Program>? _factory;
    private PostgreSqlContainer? _postgres;

    public string BaseUrl { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        // Ensures the Chromium build Playwright needs is present without depending on the
        // pwsh-only `playwright.ps1 install` script, so `dotnet test`/`dotnet run` work as-is.
        Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (Environment.GetEnvironmentVariable(BaseUrlEnvVar) is { Length: > 0 } externalUrl)
        {
            BaseUrl = externalUrl.TrimEnd('/');
            return;
        }

        _postgres = new PostgreSqlBuilder(
            "docker.io/library/postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
        ).Build();
        await _postgres.StartAsync();

        var httpPort = GetFreeTcpPort();
        var metricsPort = GetFreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{httpPort.ToString(CultureInfo.InvariantCulture)}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // Program.cs configures Kestrel itself from these two settings (see its comment on
            // why that - rather than ASPNETCORE_URLS/UseUrls - is the source of truth here).
            builder.UseSetting("HTTP_PORT", httpPort.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("METRICS_PORT", metricsPort.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("ConnectionStrings:AppDb", _postgres.GetConnectionString());
            builder.UseSetting("Auth:Disabled", "true");
            // Deliberately unreachable - see class remarks.
            builder.UseSetting("Fhir:BaseUrl", "http://127.0.0.1:1/fhir");
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
