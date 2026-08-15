using System.Net;
using Hl7.Fhir.Model;
using list.Data;
using list.Models;
using list.Options;
using list.Services.Fhir;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mjml.Net;
using FhirList = Hl7.Fhir.Model.List;
using NotificationChannel = list.Models.NotificationChannel;
using Task = System.Threading.Tasks.Task;

namespace list.Services.Notify;

/// <summary>
/// Hangfire delivery job - one invocation per (list, newly-appeared patient reference) detected by
/// ScreeningListPollService. Retried automatically by Hangfire on any exception.
/// </summary>
public sealed class NotificationDeliveryService(
    FhirClientFactory clientFactory,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<NotifyMailerOptions> mailerOptions,
    INotificationChannel channel,
    ILogger<NotificationDeliveryService> logger
)
{
    private static readonly MjmlRenderer Renderer = new();

    public async Task DeliverAsync(
        string listId,
        string patientReference,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        var list = await client.ReadAsync<FhirList>($"List/{listId}", ct: ct);
        if (list is null)
        {
            logger.LogWarning(
                "List/{ListId} could not be read - skipping notification for {PatientReference}",
                listId,
                patientReference
            );
            return;
        }

        var studyId = list.GetReferenceExtension(FhirConstants.UrlListBelongsToStudy)
            ?.GetReferencedId();
        var study = studyId is not null
            ? await client.ReadAsync<ResearchStudy>($"ResearchStudy/{studyId}", ct: ct)
            : null;

        var studyAcronym = study?.GetStudyAcronym() ?? listId;
        var trialIdentifier = study?.GetTrialIdentifier();

        if (trialIdentifier is null)
        {
            logger.LogWarning(
                "List/{ListId} has no resolvable trial identifier - skipping notification for {PatientReference}",
                listId,
                patientReference
            );
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var recipients = await db
            .NotificationRecipients.Where(r =>
                r.TrialIdentifierSystem == trialIdentifier.System
                && r.TrialIdentifierValue == trialIdentifier.Value
                && r.Channel == NotificationChannel.Email
            )
            .Select(r => r.Email)
            .ToListAsync(ct);

        if (recipients.Count == 0)
        {
            logger.LogInformation(
                "No notification recipients configured for trial {System}|{Value} - nothing to send for {PatientReference}",
                trialIdentifier.System,
                trialIdentifier.Value,
                patientReference
            );
            return;
        }

        var screeningListUrl = mailerOptions.Value.ScreeningListLinkTemplate.Replace(
            "[list_id]",
            listId
        );
        var subject = mailerOptions.Value.SubjectTemplate.Replace("[study_acronym]", studyAcronym);
        var html = RenderHtml(studyAcronym, screeningListUrl);

        foreach (var recipient in recipients)
        {
            await channel.SendAsync(recipient, subject, html, ct);
            await RecordSentAsync(client, patientReference, recipient, subject, ct);
        }
    }

    private static string RenderHtml(string studyAcronym, string screeningListUrl)
    {
        var encodedAcronym = WebUtility.HtmlEncode(studyAcronym);
        var mjml = $"""
            <mjml>
              <mj-body background-color="#f4f4f4">
                <mj-section background-color="#ffffff" padding="24px">
                  <mj-column>
                    <mj-text font-size="18px" font-weight="bold" color="#1a1a1a">
                      {encodedAcronym}: new screening suggestion
                    </mj-text>
                    <mj-text font-size="14px" color="#4a4a4a" line-height="1.5">
                      A new patient has been added to the screening list for <b>{encodedAcronym}</b>.
                    </mj-text>
                    <mj-button background-color="#2f6fed" href="{screeningListUrl}">
                      Open screening list
                    </mj-button>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        var result = Renderer.Render(mjml);
        return result.Html;
    }

    private static async Task RecordSentAsync(
        Hl7.Fhir.Rest.FhirClient client,
        string patientReference,
        string recipient,
        string subject,
        CancellationToken ct
    )
    {
        var communication = new Communication
        {
            Status = EventStatus.Completed,
            Subject = new ResourceReference(patientReference),
            Sent = DateTimeOffset.UtcNow.ToString("O"),
        };
        communication.Category.Add(
            new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/communication-category",
                "notification",
                "Notification",
                null
            )
        );
        communication.Recipient.Add(new ResourceReference { Display = recipient });
        communication.Payload.Add(
            new Communication.PayloadComponent { Content = new FhirString(subject) }
        );

        await client.CreateAsync(communication, ct);
    }
}
