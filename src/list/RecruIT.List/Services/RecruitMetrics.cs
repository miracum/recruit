using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;

namespace RecruIT.List.Services;

/// <summary>
/// recruIT's own metrics, next to the ASP.NET Core, Blazor, HttpClient (which includes every FHIR
/// request, see FhirClientFactory), Npgsql and runtime ones registered in Program.cs. Exported at
/// /metrics with Prometheus naming - dots become underscores, plus a unit suffix (`_seconds`) for
/// histograms and `_total` for counters - and charted by charts/recruit/files/dashboards. A
/// singleton, since instruments are meant to be created once and shared.
/// </summary>
public sealed class RecruitMetrics
{
    public const string MeterName = "RecruIT.List";

    private readonly Histogram<double> _detectionDuration;
    private readonly Counter<long> _detectionFailures;
    private readonly Counter<long> _recommendationsDetected;
    private readonly Histogram<double> _emailSendDuration;
    private readonly Counter<long> _emailDeliveriesDropped;
    private readonly Counter<long> _statusChanges;

    public RecruitMetrics(
        IMeterFactory meterFactory,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<RecruitMetrics> logger
    )
    {
        var meter = meterFactory.Create(MeterName);

        _detectionDuration = meter.CreateHistogram(
            "recruit.notifications.detection.duration",
            unit: "s",
            description: "How long one run of the notification detector took, across every screening list.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60],
            }
        );
        _detectionFailures = meter.CreateCounter<long>(
            "recruit.notifications.detection.failures",
            unit: "{list}",
            description: "Screening lists the notification detector failed to check - retried on its next run."
        );
        _recommendationsDetected = meter.CreateCounter<long>(
            "recruit.notifications.recommendations",
            unit: "{patient}",
            description: "Newly recommended patients the notification detector found, by trial."
        );
        _emailSendDuration = meter.CreateHistogram(
            "recruit.notifications.email.send.duration",
            unit: "s",
            description: "How long sending one notification email took, by outcome.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30],
            }
        );
        _emailDeliveriesDropped = meter.CreateCounter<long>(
            "recruit.notifications.email.dropped",
            unit: "{delivery}",
            description: "Email notifications given up on after failing to send for too long."
        );
        // Counted from the database on every scrape, rather than remembered from the email
        // sender's last run: that runs on whichever replica Hangfire picks, so every other
        // replica's remembered value would be stale. One indexed COUNT (see AppDbContext's
        // Channel/SentAt index) per scrape.
        meter.CreateObservableGauge(
            "recruit.notifications.email.pending",
            () =>
            {
                try
                {
                    using var db = dbContextFactory.CreateDbContext();
                    return
                    [
                        new Measurement<long>(
                            db.NotificationDeliveries.LongCount(d =>
                                d.Channel == NotificationChannel.Email && d.SentAt == null
                            )
                        ),
                    ];
                }
                catch (Exception ex)
                {
                    // No value this scrape rather than a wrong one, or a failed scrape.
                    logger.LogWarning(ex, "Could not count pending notification emails");
                    return Array.Empty<Measurement<long>>();
                }
            },
            unit: "{delivery}",
            description: "Email notifications not sent yet - waiting for their digest, or for a retry after failing."
        );
        _statusChanges = meter.CreateCounter<long>(
            "recruit.screening.status_changes",
            unit: "{change}",
            description: "Recruitment status changes saved by users, by the new status."
        );
    }

    public void RecordDetectionRun(TimeSpan duration, bool succeeded) =>
        _detectionDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("outcome", Outcome(succeeded))
        );

    public void RecordDetectionFailure() => _detectionFailures.Add(1);

    public void RecordRecommendations(int count, string trial) =>
        _recommendationsDetected.Add(count, new KeyValuePair<string, object?>("trial", trial));

    public void RecordEmailSend(TimeSpan duration, bool succeeded) =>
        _emailSendDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("outcome", Outcome(succeeded))
        );

    public void RecordEmailDeliveriesDropped(int count) => _emailDeliveriesDropped.Add(count);

    public void RecordStatusChange(string newStatus) =>
        _statusChanges.Add(1, new KeyValuePair<string, object?>("status", newStatus));

    private static string Outcome(bool succeeded) => succeeded ? "success" : "failure";
}
