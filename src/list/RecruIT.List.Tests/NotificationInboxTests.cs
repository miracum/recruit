using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Services.Access;
using RecruIT.List.Services.Notify;
using static RecruIT.List.Tests.TestUsers;

namespace RecruIT.List.Tests;

/// <summary>
/// The read/unread mutations behind the notifications page and the email link
/// (NotificationSubscriptionService). The feed listing itself isn't covered here: it orders by
/// DateTimeOffset server-side, which Sqlite can't translate (see ScreeningNoteService).
/// </summary>
public sealed class NotificationInboxTests
{
    private static readonly TrialIdentifier TrialA = new(
        "https://fhir.example.org/study-id",
        "STUDY-A"
    );
    private static readonly TrialIdentifier TrialB = new(
        "https://fhir.example.org/study-id",
        "STUDY-B"
    );

    private const string AliceSub = "alice-sub";
    private const string AliceEmail = "alice@example.com";
    private const string BobSub = "bob-sub";
    private const string BobEmail = "bob@example.com";

    private static readonly ClaimsPrincipal Alice = CreateUser(sub: AliceSub, email: AliceEmail);
    private static readonly ClaimsPrincipal Bob = CreateUser(sub: BobSub, email: BobEmail);

    private static async Task<(
        NotificationSubscriptionService Service,
        SqliteDbContextFactory Factory
    )> CreateServiceAsync()
    {
        var factory = new SqliteDbContextFactory();
        foreach (var trial in new[] { TrialA, TrialB })
        {
            await SeedGrantAsync(factory, trial, AliceEmail, TrialPermissionLevel.Viewer, AliceSub);
            await SeedGrantAsync(factory, trial, BobEmail, TrialPermissionLevel.Viewer, BobSub);
        }

        var access = new TrialAccessService(
            factory,
            new FakeStringLocalizer(),
            NullLogger<TrialAccessService>.Instance
        );
        return (new NotificationSubscriptionService(factory, access), factory);
    }

    private static async Task<Guid> SeedEventAsync(
        SqliteDbContextFactory factory,
        TrialIdentifier trial
    )
    {
        await using var db = factory.CreateDbContext();
        var id = Guid.NewGuid();
        db.NotificationEvents.Add(
            new NotificationEvent
            {
                Id = id,
                TrialIdentifier = trial.ToToken(),
                PatientReference = $"ResearchSubject/{id}",
                PatientDisplayName = "Test Patient",
                OccurredAt = DateTimeOffset.UtcNow,
                DedupeKey = id.ToString(),
            }
        );
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Seeds the InApp/Email delivery pair the detector writes for one (event, subscription) - the
    /// Email one already sent as part of emailDigestId, if given.
    /// </summary>
    private static async Task<Guid> SeedDeliveriesAsync(
        SqliteDbContextFactory factory,
        Guid eventId,
        Guid subscriptionId,
        string subjectId,
        string email,
        Guid? emailDigestId = null
    )
    {
        await using var db = factory.CreateDbContext();
        var inAppId = Guid.NewGuid();
        db.NotificationDeliveries.AddRange(
            new NotificationDelivery
            {
                Id = inAppId,
                NotificationEventId = eventId,
                NotificationSubscriptionId = subscriptionId,
                SubjectId = subjectId,
                Email = email,
                Channel = NotificationChannel.InApp,
                ScheduledFor = DateTimeOffset.UtcNow,
            },
            new NotificationDelivery
            {
                Id = Guid.NewGuid(),
                NotificationEventId = eventId,
                NotificationSubscriptionId = subscriptionId,
                SubjectId = subjectId,
                Email = email,
                Channel = NotificationChannel.Email,
                ScheduledFor = DateTimeOffset.UtcNow,
                SentAt = emailDigestId is null ? null : DateTimeOffset.UtcNow,
                EmailDigestId = emailDigestId,
            }
        );
        await db.SaveChangesAsync();
        return inAppId;
    }

    private static async Task<bool> IsReadAsync(SqliteDbContextFactory factory, Guid deliveryId)
    {
        await using var db = factory.CreateDbContext();
        var delivery = await db.NotificationDeliveries.SingleAsync(d => d.Id == deliveryId);
        return delivery.ReadAt is not null;
    }

    [Fact]
    public async Task Marking_read_and_unread_updates_the_unread_count()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, factory) = await CreateServiceAsync();
        using var _ = factory;
        var subscription = Guid.NewGuid();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var eventId = await SeedEventAsync(factory, TrialA);
            ids.Add(
                await SeedDeliveriesAsync(factory, eventId, subscription, AliceSub, AliceEmail)
            );
        }

        Assert.Equal(3, await service.GetUnreadInAppCountAsync(Alice, ct));

        await service.MarkReadAsync([ids[0], ids[1]], Alice, ct);
        Assert.Equal(1, await service.GetUnreadInAppCountAsync(Alice, ct));

        await service.MarkUnreadAsync([ids[0]], Alice, ct);
        Assert.Equal(2, await service.GetUnreadInAppCountAsync(Alice, ct));
        Assert.False(await IsReadAsync(factory, ids[0]));
        Assert.True(await IsReadAsync(factory, ids[1]));
    }

    [Fact]
    public async Task Read_state_changes_never_touch_another_users_deliveries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, factory) = await CreateServiceAsync();
        using var _ = factory;
        var eventId = await SeedEventAsync(factory, TrialA);
        var bobsDelivery = await SeedDeliveriesAsync(
            factory,
            eventId,
            Guid.NewGuid(),
            BobSub,
            BobEmail
        );

        // Alice naming Bob's delivery id explicitly, and clearing "all" of hers.
        await service.MarkReadAsync([bobsDelivery], Alice, ct);
        await service.MarkAllReadAsync(Alice, ct: ct);
        Assert.False(await IsReadAsync(factory, bobsDelivery));

        await service.MarkReadAsync([bobsDelivery], Bob, ct);
        await service.MarkUnreadAsync([bobsDelivery], Alice, ct);
        Assert.True(await IsReadAsync(factory, bobsDelivery));
    }

    [Fact]
    public async Task Mark_all_read_can_be_limited_to_one_trial()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, factory) = await CreateServiceAsync();
        using var _ = factory;
        var subscription = Guid.NewGuid();
        var inTrialA = await SeedDeliveriesAsync(
            factory,
            await SeedEventAsync(factory, TrialA),
            subscription,
            AliceSub,
            AliceEmail
        );
        var inTrialB = await SeedDeliveriesAsync(
            factory,
            await SeedEventAsync(factory, TrialB),
            Guid.NewGuid(),
            AliceSub,
            AliceEmail
        );

        await service.MarkAllReadAsync(Alice, TrialA.ToToken(), ct);
        Assert.True(await IsReadAsync(factory, inTrialA));
        Assert.False(await IsReadAsync(factory, inTrialB));

        await service.MarkAllReadAsync(Alice, ct: ct);
        Assert.True(await IsReadAsync(factory, inTrialB));
        Assert.Equal(0, await service.GetUnreadInAppCountAsync(Alice, ct));
    }

    [Fact]
    public async Task Opening_an_email_marks_exactly_the_items_it_covered_read()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, factory) = await CreateServiceAsync();
        using var _ = factory;
        var subscription = Guid.NewGuid();
        var digestId = Guid.NewGuid();
        var emailed1 = await SeedDeliveriesAsync(
            factory,
            await SeedEventAsync(factory, TrialA),
            subscription,
            AliceSub,
            AliceEmail,
            digestId
        );
        var emailed2 = await SeedDeliveriesAsync(
            factory,
            await SeedEventAsync(factory, TrialA),
            subscription,
            AliceSub,
            AliceEmail,
            digestId
        );
        // Detected after that email went out - still pending its own email.
        var notYetEmailed = await SeedDeliveriesAsync(
            factory,
            await SeedEventAsync(factory, TrialA),
            subscription,
            AliceSub,
            AliceEmail
        );

        var trials = await service.MarkEmailDigestReadAsync(digestId, Alice, ct);

        Assert.Equal([TrialA.ToToken()], trials);
        Assert.True(await IsReadAsync(factory, emailed1));
        Assert.True(await IsReadAsync(factory, emailed2));
        Assert.False(await IsReadAsync(factory, notYetEmailed));
    }

    [Fact]
    public async Task Someone_elses_or_an_unknown_email_link_changes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, factory) = await CreateServiceAsync();
        using var _ = factory;
        var digestId = Guid.NewGuid();
        var alicesItem = await SeedDeliveriesAsync(
            factory,
            await SeedEventAsync(factory, TrialA),
            Guid.NewGuid(),
            AliceSub,
            AliceEmail,
            digestId
        );

        // e.g. Alice forwarded the email to Bob, who then clicked it.
        Assert.Empty(await service.MarkEmailDigestReadAsync(digestId, Bob, ct));
        Assert.Empty(await service.MarkEmailDigestReadAsync(Guid.NewGuid(), Alice, ct));
        Assert.False(await IsReadAsync(factory, alicesItem));
    }
}
