using RecruIT.List.Models;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// Pure scheduling logic, deliberately free of any FHIR/DB/clock dependency so it's trivially
/// unit-testable: given a subscriber's frequency preference and the instant an event occurred,
/// when should their next digest email go out? Several events landing in the same Daily/Weekly/
/// Monthly slot compute the same ScheduledFor instant, which is what lets
/// NotificationSenderService's "group due deliveries by subscription" batch them into one digest
/// email without any extra bookkeeping.
/// </summary>
public static class NotificationScheduling
{
    /// <summary>
    /// Fixed local send time for Monthly digests, which (unlike Daily/Weekly) have no
    /// user-configurable time - see NotificationSubscribeButton.razor's Monthly note.
    /// </summary>
    public static readonly TimeOnly MonthlyDigestTime = new(8, 0);

    /// <summary>
    /// Instant emails go out immediately (ScheduledFor = the event's own OccurredAt); everything
    /// else is scheduled for the subscriber's next digest slot, evaluated in their own time zone
    /// (timeZoneId, an IANA id captured client-side at subscribe time - see
    /// NotificationSubscribeButton.razor).
    /// </summary>
    public static DateTimeOffset ComputeEmailScheduledFor(
        NotificationFrequency frequency,
        DayOfWeek? dayOfWeek,
        TimeOnly? timeOfDay,
        string timeZoneId,
        DateTimeOffset occurredAt
    )
    {
        if (frequency == NotificationFrequency.Instant)
        {
            return occurredAt;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var referenceLocal = TimeZoneInfo.ConvertTime(occurredAt, timeZone).DateTime;

        var candidateLocal = frequency switch
        {
            NotificationFrequency.Daily => NextDailyOccurrence(referenceLocal, timeOfDay!.Value),
            NotificationFrequency.Weekly => NextWeeklyOccurrence(
                referenceLocal,
                dayOfWeek!.Value,
                timeOfDay!.Value
            ),
            NotificationFrequency.Monthly => NextMonthlyOccurrence(referenceLocal),
            _ => throw new ArgumentOutOfRangeException(
                nameof(frequency),
                frequency,
                "Unhandled notification frequency."
            ),
        };

        return new DateTimeOffset(candidateLocal, timeZone.GetUtcOffset(candidateLocal));
    }

    private static DateTime NextDailyOccurrence(DateTime referenceLocal, TimeOnly timeOfDay)
    {
        var candidate = referenceLocal.Date + timeOfDay.ToTimeSpan();
        return candidate <= referenceLocal ? candidate.AddDays(1) : candidate;
    }

    private static DateTime NextWeeklyOccurrence(
        DateTime referenceLocal,
        DayOfWeek targetDay,
        TimeOnly timeOfDay
    )
    {
        var candidate = referenceLocal.Date + timeOfDay.ToTimeSpan();
        var daysUntilTarget = ((int)targetDay - (int)candidate.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysUntilTarget);
        return candidate <= referenceLocal ? candidate.AddDays(7) : candidate;
    }

    private static DateTime NextMonthlyOccurrence(DateTime referenceLocal)
    {
        var candidate =
            new DateTime(referenceLocal.Year, referenceLocal.Month, 1)
            + MonthlyDigestTime.ToTimeSpan();
        return candidate <= referenceLocal ? candidate.AddMonths(1) : candidate;
    }
}
