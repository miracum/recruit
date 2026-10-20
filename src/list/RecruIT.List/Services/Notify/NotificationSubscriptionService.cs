using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Services.Access;
using RecruIT.List.Services.Auth;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// CRUD for a user's own per-trial NotificationSubscription, plus reads for the in-app bell/feed
/// (NotificationDelivery rows on the InApp channel). Mirrors TrialAccessService's shape, but every
/// operation here only ever touches the caller's own row - there's no email-invite/backfill case
/// like TrialAccessGrant has, since a subscription can only be created by the subscriber themselves
/// while authenticated. A subscription outlives trial-access revocation (unsubscribing is a
/// separate, explicit action), so the read paths that surface patient data - the feed and its
/// unread count - re-check current access via TrialAccessService and filter accordingly; see
/// NotificationSenderService for the equivalent check on the send path.
/// </summary>
public sealed class NotificationSubscriptionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    TrialAccessService accessService
)
{
    /// <summary>Upper bound on how many unread items the in-app feed ever returns in one page.</summary>
    private const int MaxFeedItems = 100;

    public async Task<NotificationSubscription?> GetAsync(
        ClaimsPrincipal user,
        TrialIdentifier trialIdentifier,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = user.GetUserKeys();
        if (subjectId is null)
        {
            return null;
        }

        var token = trialIdentifier.ToToken();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db
            .NotificationSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TrialIdentifier == token && s.SubjectId == subjectId, ct);
    }

    public async Task SubscribeAsync(
        ClaimsPrincipal user,
        TrialIdentifier trialIdentifier,
        NotificationFrequency frequency,
        DayOfWeek? dayOfWeek,
        TimeOnly? timeOfDay,
        string timeZoneId,
        CancellationToken ct = default
    )
    {
        var (subjectId, email) = user.GetUserKeys();
        if (subjectId is null || email is null)
        {
            throw new InvalidOperationException(
                "Cannot subscribe without both a sub and an email claim."
            );
        }

        var token = trialIdentifier.ToToken();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.NotificationSubscriptions.FirstOrDefaultAsync(
            s => s.TrialIdentifier == token && s.SubjectId == subjectId,
            ct
        );

        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            db.NotificationSubscriptions.Add(
                new NotificationSubscription
                {
                    Id = Guid.NewGuid(),
                    TrialIdentifier = token,
                    SubjectId = subjectId,
                    Email = email,
                    Frequency = frequency,
                    DayOfWeek = dayOfWeek,
                    TimeOfDay = timeOfDay,
                    TimeZoneId = timeZoneId,
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
        }
        else
        {
            existing.Email = email;
            existing.Frequency = frequency;
            existing.DayOfWeek = dayOfWeek;
            existing.TimeOfDay = timeOfDay;
            existing.TimeZoneId = timeZoneId;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UnsubscribeAsync(
        ClaimsPrincipal user,
        TrialIdentifier trialIdentifier,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = user.GetUserKeys();
        if (subjectId is null)
        {
            return;
        }

        var token = trialIdentifier.ToToken();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.NotificationSubscriptions.FirstOrDefaultAsync(
            s => s.TrialIdentifier == token && s.SubjectId == subjectId,
            ct
        );
        if (existing is null)
        {
            return;
        }

        db.NotificationSubscriptions.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetUnreadInAppCountAsync(
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = user.GetUserKeys();
        if (subjectId is null)
        {
            return 0;
        }

        var accessibleTokens = await GetAccessibleTokensAsync(user, ct);
        if (accessibleTokens is not null && accessibleTokens.Count == 0)
        {
            return 0;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var unread = db
            .NotificationDeliveries.AsNoTracking()
            .Where(d =>
                d.SubjectId == subjectId
                && d.Channel == NotificationChannel.InApp
                && d.ReadAt == null
            );

        if (accessibleTokens is null)
        {
            return await unread.CountAsync(ct);
        }

        return await unread
            .Join(
                db.NotificationEvents,
                d => d.NotificationEventId,
                e => e.Id,
                (d, e) => e.TrialIdentifier
            )
            .CountAsync(token => accessibleTokens.Contains(token), ct);
    }

    public async Task<IReadOnlyList<NotificationFeedItemDto>> ListInAppFeedAsync(
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = user.GetUserKeys();
        if (subjectId is null)
        {
            return [];
        }

        var accessibleTokens = await GetAccessibleTokensAsync(user, ct);
        if (accessibleTokens is not null && accessibleTokens.Count == 0)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var items = await db
            .NotificationDeliveries.AsNoTracking()
            .Where(d =>
                d.SubjectId == subjectId
                && d.Channel == NotificationChannel.InApp
                && d.ReadAt == null
            )
            .Join(
                db.NotificationEvents,
                d => d.NotificationEventId,
                e => e.Id,
                (d, e) =>
                    new
                    {
                        d.Id,
                        e.TrialIdentifier,
                        e.PatientDisplayName,
                        e.OccurredAt,
                    }
            )
            .OrderByDescending(joined => joined.OccurredAt)
            .Take(MaxFeedItems)
            .Select(joined => new NotificationFeedItemDto(
                joined.Id,
                joined.TrialIdentifier,
                joined.PatientDisplayName,
                joined.OccurredAt
            ))
            .ToListAsync(ct);

        // A subscription outlives trial-access revocation - drop anything for a trial the caller
        // can no longer access rather than showing a revoked coordinator patient data for a trial
        // they're no longer on. Filtered after the DB-level Take, so a caller with recently
        // revoked access may see fewer than MaxFeedItems even when more unread rows exist; that's
        // an acceptable trade-off for keeping this a single indexed query.
        return accessibleTokens is null
            ? items
            : [.. items.Where(i => accessibleTokens.Contains(i.TrialIdentifier))];
    }

    /// <summary>
    /// null means "admin, everything is accessible" (see TrialAccessService.CanAccessTrial) - kept
    /// as null rather than expanded to a full trial list so callers can distinguish "no
    /// restriction" from "restricted to zero trials" without a separate admin check.
    /// </summary>
    private async Task<IReadOnlySet<string>?> GetAccessibleTokensAsync(
        ClaimsPrincipal user,
        CancellationToken ct
    )
    {
        var accessibleTrials = await accessService.GetAccessibleTrialIdentifiersAsync(user, ct);
        return accessibleTrials?.Select(t => t.ToToken()).ToHashSet();
    }

    public async Task MarkReadAsync(
        Guid deliveryId,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = user.GetUserKeys();
        if (subjectId is null)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var delivery = await db.NotificationDeliveries.FirstOrDefaultAsync(
            d => d.Id == deliveryId && d.SubjectId == subjectId,
            ct
        );
        if (delivery is null)
        {
            return;
        }

        delivery.ReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
