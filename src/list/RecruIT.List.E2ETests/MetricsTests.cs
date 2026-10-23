namespace RecruIT.List.E2ETests;

/// <summary>
/// The Prometheus endpoint the chart's Grafana dashboard is built on. It can fail on every scrape
/// without the app itself noticing - e.g. when the exporter package falls out of step with the
/// OpenTelemetry SDK - so this checks it actually serves recruIT's metrics.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class MetricsTests(AppFixture app)
{
    [Fact]
    public async Task Metrics_endpoint_serves_recruits_own_and_the_frameworks_metrics()
    {
        if (app.MetricsUrl is null)
        {
            Assert.Skip("No metrics endpoint to check - set E2E_METRICS_URL.");
        }

        using var http = new HttpClient();
        using var response = await http.GetAsync(
            app.MetricsUrl,
            TestContext.Current.CancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(
            response.IsSuccessStatusCode,
            $"Scraping metrics returned {response.StatusCode}."
        );
        Assert.Contains("recruit_notifications_email_pending", body);
        // A histogram, since writing their bucket bounds is what broke with a mismatched exporter.
        Assert.Contains("http_server_request_duration_seconds_bucket", body);
    }
}
