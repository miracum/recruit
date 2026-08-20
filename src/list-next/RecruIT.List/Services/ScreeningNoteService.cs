using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Resources;
using RecruIT.List.Services.Access;
using RecruIT.List.Services.Auth;
using RecruIT.List.Services.Fhir;

namespace RecruIT.List.Services;

/// <summary>
/// Screening notes, stored as immutable, insert-only rows in Postgres (screening_notes) - keyed by
/// ResearchSubject's business identifier, not its FHIR logical id (see
/// RecruIT.List.Services.Fhir.FhirConstants.UrlResearchSubjectIdentifier). Replaces the earlier design of appending FHIR
/// Annotation extensions to ResearchSubject, which had an unmitigated lost-update race on
/// concurrent adds (read-then-PATCH with no ETag) and polluted the resource's FHIR version history
/// on every note-only edit.
/// </summary>
public sealed class ScreeningNoteService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    TrialAccessService accessService,
    IStringLocalizer<SharedResources> localizer,
    ILogger<ScreeningNoteService> logger
)
{
    public async Task AddNoteAsync(
        string researchSubjectIdentifier,
        string text,
        TrialIdentifier trialIdentifier,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        if (!await accessService.CanPatchResearchSubjectAsync(user, trialIdentifier, ct))
        {
            throw new UnauthorizedAccessException(
                localizer["App.Errors.NotAuthorizedUpdatePatient"]
            );
        }

        var subjectId = user.GetSubjectId();
        var authorDisplayName = GetAuthorDisplayName(user);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        db.ScreeningNotes.Add(
            new ScreeningNote
            {
                Id = Guid.NewGuid(),
                ResearchSubjectIdentifier = researchSubjectIdentifier,
                TrialIdentifier = trialIdentifier.ToToken(),
                Text = text,
                AuthorSubjectId = subjectId,
                AuthorDisplayName = authorDisplayName,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to save screening note for ResearchSubject identifier {ResearchSubjectIdentifier}",
                researchSubjectIdentifier
            );
            throw new FhirAccessException(localizer["App.Errors.PatientRecordUpdateFailed"], ex);
        }
    }

    /// <summary>All notes ever added to this subject, newest first.</summary>
    public async Task<IReadOnlyList<ScreeningNoteDto>> GetNotesAsync(
        string researchSubjectIdentifier,
        CancellationToken ct = default
    )
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var notes = await db
            .ScreeningNotes.AsNoTracking()
            .Where(n => n.ResearchSubjectIdentifier == researchSubjectIdentifier)
            .Select(n => new ScreeningNoteDto
            {
                Text = n.Text,
                Author = n.AuthorDisplayName,
                Time = n.CreatedAt,
            })
            .ToListAsync(ct);

        // Ordered client-side rather than via OrderByDescending in the query above - Sqlite (used
        // by this project's unit tests) can't translate ordering by DateTimeOffset server-side;
        // Postgres can, but a per-subject notes list is small enough that this costs nothing
        // either way.
        return notes.OrderByDescending(n => n.Time).ToList();
    }

    private static string GetAuthorDisplayName(ClaimsPrincipal user) =>
        user.GetDisplayName() ?? user.GetEmail() ?? "unknown";
}
