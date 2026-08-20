using RecruIT.List.Models;
using RecruIT.List.Services.Notify;

namespace RecruIT.List.Tests;

public sealed class NotificationSchedulingTests
{
    private const string Berlin = "Europe/Berlin";

    [Fact]
    public void Instant_ReturnsOccurredAtUnchanged()
    {
        var occurredAt = new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Instant,
            dayOfWeek: null,
            timeOfDay: null,
            Berlin,
            occurredAt
        );

        Assert.Equal(occurredAt, result);
    }

    [Fact]
    public void Daily_BeforeTimeOfDay_SchedulesLaterTheSameDay()
    {
        // 2026-03-10 09:00 UTC = 10:00 Berlin (CET, UTC+1) - before the 18:00 digest time.
        var occurredAt = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Daily,
            dayOfWeek: null,
            timeOfDay: new TimeOnly(18, 0),
            Berlin,
            occurredAt
        );

        AssertLocal(result, Berlin, new DateTime(2026, 3, 10, 18, 0, 0));
    }

    [Fact]
    public void Daily_AfterTimeOfDay_SchedulesTheNextDay()
    {
        // 2026-03-10 20:00 UTC = 21:00 Berlin - after the 18:00 digest time.
        var occurredAt = new DateTimeOffset(2026, 3, 10, 20, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Daily,
            dayOfWeek: null,
            timeOfDay: new TimeOnly(18, 0),
            Berlin,
            occurredAt
        );

        AssertLocal(result, Berlin, new DateTime(2026, 3, 11, 18, 0, 0));
    }

    [Fact]
    public void Weekly_OnTargetDayBeforeTimeOfDay_SchedulesLaterTheSameDay()
    {
        // 2026-03-10 is a Tuesday.
        var occurredAt = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Weekly,
            DayOfWeek.Tuesday,
            new TimeOnly(18, 0),
            Berlin,
            occurredAt
        );

        AssertLocal(result, Berlin, new DateTime(2026, 3, 10, 18, 0, 0));
    }

    [Fact]
    public void Weekly_OnTargetDayAfterTimeOfDay_SchedulesTheFollowingWeek()
    {
        var occurredAt = new DateTimeOffset(2026, 3, 10, 20, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Weekly,
            DayOfWeek.Tuesday,
            new TimeOnly(18, 0),
            Berlin,
            occurredAt
        );

        AssertLocal(result, Berlin, new DateTime(2026, 3, 17, 18, 0, 0));
    }

    [Fact]
    public void Weekly_OnEarlierWeekday_SchedulesLaterThatSameWeek()
    {
        // Tuesday 2026-03-10, subscriber wants Friday - 3 days later.
        var occurredAt = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Weekly,
            DayOfWeek.Friday,
            new TimeOnly(9, 0),
            Berlin,
            occurredAt
        );

        AssertLocal(result, Berlin, new DateTime(2026, 3, 13, 9, 0, 0));
    }

    [Fact]
    public void Weekly_OnLaterWeekday_SchedulesEarlierNextWeek()
    {
        // Friday 2026-03-13, subscriber wants Tuesday - wraps to the following week.
        var occurredAt = new DateTimeOffset(2026, 3, 13, 9, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Weekly,
            DayOfWeek.Tuesday,
            new TimeOnly(9, 0),
            Berlin,
            occurredAt
        );

        AssertLocal(result, Berlin, new DateTime(2026, 3, 17, 9, 0, 0));
    }

    [Fact]
    public void Monthly_BeforeDigestTime_SchedulesTheFirstOfTheCurrentMonth()
    {
        var occurredAt = new DateTimeOffset(2026, 3, 15, 5, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Monthly,
            dayOfWeek: null,
            timeOfDay: null,
            Berlin,
            occurredAt
        );

        AssertLocal(result, Berlin, new DateTime(2026, 4, 1) + NotificationScheduling.MonthlyDigestTime.ToTimeSpan());
    }

    [Fact]
    public void Monthly_OnTheFirstBeforeDigestTime_SchedulesLaterTheSameDay()
    {
        // 2026-03-01 06:00 Berlin (CET, UTC+1) is before the 08:00 digest time.
        var occurredAt = new DateTimeOffset(2026, 3, 1, 5, 0, 0, TimeSpan.Zero);

        var result = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Monthly,
            dayOfWeek: null,
            timeOfDay: null,
            Berlin,
            occurredAt
        );

        AssertLocal(result, Berlin, new DateTime(2026, 3, 1) + NotificationScheduling.MonthlyDigestTime.ToTimeSpan());
    }

    [Fact]
    public void MultipleEventsInTheSameSlot_ComputeTheSameScheduledFor()
    {
        // Same day, both before the 18:00 Daily digest time - should collapse onto one instant,
        // which is what lets NotificationSenderService batch them into a single email.
        var morning = new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
        var noon = new DateTimeOffset(2026, 3, 10, 11, 0, 0, TimeSpan.Zero);
        var timeOfDay = new TimeOnly(18, 0);

        var first = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Daily,
            null,
            timeOfDay,
            Berlin,
            morning
        );
        var second = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Daily,
            null,
            timeOfDay,
            Berlin,
            noon
        );

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentTimeZones_ProduceDifferentUtcInstantsForTheSameLocalTime()
    {
        var occurredAt = new DateTimeOffset(2026, 3, 10, 5, 0, 0, TimeSpan.Zero);
        var timeOfDay = new TimeOnly(9, 0);

        var berlin = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Daily,
            null,
            timeOfDay,
            "Europe/Berlin",
            occurredAt
        );
        var tokyo = NotificationScheduling.ComputeEmailScheduledFor(
            NotificationFrequency.Daily,
            null,
            timeOfDay,
            "Asia/Tokyo",
            occurredAt
        );

        Assert.NotEqual(berlin.UtcDateTime, tokyo.UtcDateTime);
    }

    private static void AssertLocal(DateTimeOffset actual, string timeZoneId, DateTime expectedLocal)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var actualLocal = TimeZoneInfo.ConvertTime(actual, timeZone).DateTime;
        Assert.Equal(expectedLocal, actualLocal);
    }
}
