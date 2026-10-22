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
    /// <summary>Upper bound on how many items the in-app feed ever returns in one page.</summary>
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

    /// <summary>
    /// The caller's in-app feed - Unread newest event first, Read most recently read first (the
    /// order GitHub's notification list uses for its "Done" view too).
    /// </summary>
    public async Task<IReadOnlyList<NotificationFeedItemDto>> ListInAppFeedAsync(
        ClaimsPrincipal user,
        NotificationFeedFilter filter,
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
        var deliveries = db
            .NotificationDeliveries.AsNoTracking()
            .Where(d => d.SubjectId == subjectId && d.Channel == NotificationChannel.InApp);
        deliveries =
            filter == NotificationFeedFilter.Unread
                ? deliveries.Where(d => d.ReadAt == null)
                : deliveries.Where(d => d.ReadAt != null);

        var joined = deliveries.Join(
            db.NotificationEvents,
            d => d.NotificationEventId,
            e => e.Id,
            (d, e) =>
                new
                {
                    d.Id,
                    d.ReadAt,
                    e.TrialIdentifier,
                    e.PatientDisplayName,
                    e.OccurredAt,
                }
        );
        var ordered =
            filter == NotificationFeedFilter.Unread
                ? joined.OrderByDescending(j => j.OccurredAt)
                : joined.OrderByDescending(j => j.ReadAt);

        var items = await ordered
            .Take(MaxFeedItems)
            .Select(j => new NotificationFeedItemDto(
                j.Id,
                j.TrialIdentifier,
                j.PatientDisplayName,
                j.OccurredAt,
                j.ReadAt
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

    public Task MarkReadAsync(
        IReadOnlyCollection<Guid> deliveryIds,
        ClaimsPrincipal user,
        CancellationToken ct = default
    ) =>
        deliveryIds.Count == 0
            ? Task.CompletedTask
            : SetOwnInAppReadAtAsync(
                user,
                (_, own) => own.Where(d => d.ReadAt == null && deliveryIds.Contains(d.Id)),
                DateTimeOffset.UtcNow,
                ct
            );

    public Task MarkUnreadAsync(
        IReadOnlyCollection<Guid> deliveryIds,
        ClaimsPrincipal user,
        CancellationToken ct = default
    ) =>
        deliveryIds.Count == 0
            ? Task.CompletedTask
            : SetOwnInAppReadAtAsync(
                user,
                (_, own) => own.Where(d => d.ReadAt != null && deliveryIds.Contains(d.Id)),
                null,
                ct
            );

    /// <summary>
    /// Marks every unread item read - all of them, or only one trial's when trialIdentifierToken
    /// ("system|value") is given. Deliberately not narrowed to currently-accessible trials like the
    /// feed is: clearing items the caller can no longer even see is harmless, and keeps the
    /// unread count from resurfacing them should access be restored later.
    /// </summary>
    public Task MarkAllReadAsync(
        ClaimsPrincipal user,
        string? trialIdentifierToken = null,
        CancellationToken ct = default
    ) =>
        SetOwnInAppReadAtAsync(
            user,
            (db, own) =>
            {
                var unread = own.Where(d => d.ReadAt == null);
                return trialIdentifierToken is null
                    ? unread
                    : unread.Where(d =>
                        db.NotificationEvents.Any(e =>
                            e.Id == d.NotificationEventId
                            && e.TrialIdentifier == trialIdentifierToken
                        )
                    );
            },
            DateTimeOffset.UtcNow,
            ct
        );

    /// <summary>
    /// Marks read the caller's in-app items for exactly the events one sent digest email covered
    /// (see NotificationDelivery.EmailDigestId), and returns those events' distinct trial
    /// identifier tokens so the caller can decide where to send the user next. A digest that
    /// doesn't belong to the caller - a forwarded email, or a guessed id - matches nothing, so it
    /// changes nothing and returns an empty list, indistinguishable from an unknown id.
    /// </summary>
    public async Task<IReadOnlyList<string>> MarkEmailDigestReadAsync(
        Guid digestId,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = user.GetUserKeys();
        if (subjectId is null)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var emailed = await db
            .NotificationDeliveries.AsNoTracking()
            .Where(d =>
                d.EmailDigestId == digestId
                && d.SubjectId == subjectId
                && d.Channel == NotificationChannel.Email
            )
            .Select(d => new { d.NotificationSubscriptionId, d.NotificationEventId })
            .ToListAsync(ct);
        if (emailed.Count == 0)
        {
            return [];
        }

        // A digest is always one subscription's deliveries (NotificationSenderService groups by
        // subscription), so matching on both id sets selects exactly the emailed pairs.
        var subscriptionIds = emailed.Select(e => e.NotificationSubscriptionId).Distinct().ToList();
        var eventIds = emailed.Select(e => e.NotificationEventId).Distinct().ToList();

        await db
            .NotificationDeliveries.Where(d =>
                d.SubjectId == subjectId
                && d.Channel == NotificationChannel.InApp
                && d.ReadAt == null
                && subscriptionIds.Contains(d.NotificationSubscriptionId)
                && eventIds.Contains(d.NotificationEventId)
            )
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.ReadAt, DateTimeOffset.UtcNow), ct);

        return await db
            .NotificationEvents.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => e.TrialIdentifier)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Shared set-based update behind every read/unread mutation: always narrowed to the caller's
    /// own InApp rows first, so no filter passed in can ever reach another user's deliveries.
    /// </summary>
    private async Task SetOwnInAppReadAtAsync(
        ClaimsPrincipal user,
        Func<
            AppDbContext,
            IQueryable<NotificationDelivery>,
            IQueryable<NotificationDelivery>
        > narrow,
        DateTimeOffset? readAt,
        CancellationToken ct
    )
    {
        var (subjectId, _) = user.GetUserKeys();
        if (subjectId is null)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var own = db.NotificationDeliveries.Where(d =>
            d.SubjectId == subjectId && d.Channel == NotificationChannel.InApp
        );
        await narrow(db, own).ExecuteUpdateAsync(set => set.SetProperty(d => d.ReadAt, readAt), ct);
    }
}
