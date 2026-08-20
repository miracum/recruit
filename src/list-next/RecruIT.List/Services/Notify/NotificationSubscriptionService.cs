using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// CRUD for a user's own per-trial NotificationSubscription, plus reads for the in-app bell/feed
/// (NotificationDelivery rows on the InApp channel). Mirrors TrialAccessService's shape, but every
/// operation here only ever touches the caller's own row - there's no email-invite/backfill case
/// like TrialAccessGrant has, since a subscription can only be created by the subscriber themselves
/// while authenticated.
/// </summary>
public sealed class NotificationSubscriptionService(
    IDbContextFactory<AppDbContext> dbContextFactory
)
{
    public async Task<NotificationSubscription?> GetAsync(
        ClaimsPrincipal user,
        TrialIdentifier trialIdentifier,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = GetUserKeys(user);
        if (subjectId is null)
        {
            return null;
        }

        var token = Token(trialIdentifier);
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
        var (subjectId, email) = GetUserKeys(user);
        if (subjectId is null || email is null)
        {
            throw new InvalidOperationException(
                "Cannot subscribe without both a sub and an email claim."
            );
        }

        var token = Token(trialIdentifier);
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
        var (subjectId, _) = GetUserKeys(user);
        if (subjectId is null)
        {
            return;
        }

        var token = Token(trialIdentifier);
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
        var (subjectId, _) = GetUserKeys(user);
        if (subjectId is null)
        {
            return 0;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db
            .NotificationDeliveries.AsNoTracking()
            .CountAsync(
                d =>
                    d.SubjectId == subjectId
                    && d.Channel == NotificationChannel.InApp
                    && d.ReadAt == null,
                ct
            );
    }

    public async Task<IReadOnlyList<NotificationFeedItemDto>> ListInAppFeedAsync(
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = GetUserKeys(user);
        if (subjectId is null)
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
                    new NotificationFeedItemDto(
                        d.Id,
                        e.TrialIdentifier,
                        e.PatientDisplayName,
                        e.OccurredAt
                    )
            )
            .ToListAsync(ct);

        return items.OrderByDescending(item => item.OccurredAt).ToList();
    }

    public async Task MarkReadAsync(
        Guid deliveryId,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        var (subjectId, _) = GetUserKeys(user);
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

    private static string Token(TrialIdentifier trialIdentifier) =>
        $"{trialIdentifier.System}|{trialIdentifier.Value}";

    private static (string? SubjectId, string? Email) GetUserKeys(ClaimsPrincipal user) =>
        (
            user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value
        );
}
