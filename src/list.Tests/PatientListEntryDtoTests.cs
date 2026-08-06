using list.Models;

namespace list.Tests;

public sealed class PatientListEntryDtoTests
{
    private static PatientListEntryDto CreateEntry(DateTimeOffset? birthDate = null, DateTimeOffset? recommendedDate = null) => new()
    {
        ResearchSubjectId = "rs-1",
        PatientId = "pat-1",
        Status = "candidate",
        BirthDate = birthDate,
        RecommendedDate = recommendedDate,
    };

    [Fact]
    public void Age_is_null_when_birth_date_is_unknown()
    {
        var entry = CreateEntry();

        Assert.Null(entry.Age);
    }

    [Fact]
    public void Age_counts_a_full_year_only_after_the_birthday_has_passed_this_year()
    {
        var today = DateTimeOffset.UtcNow;

        var birthdayAlreadyPassed = today.AddYears(-30).AddDays(-10);
        Assert.Equal(30, CreateEntry(birthDate: birthdayAlreadyPassed).Age);

        var birthdayNotYetReached = today.AddYears(-30).AddDays(10);
        Assert.Equal(29, CreateEntry(birthDate: birthdayNotYetReached).Age);
    }

    [Fact]
    public void IsNew_is_true_only_within_the_configured_window()
    {
        var recentlyRecommended = CreateEntry(recommendedDate: DateTimeOffset.UtcNow.AddDays(-2));
        var oldRecommendation = CreateEntry(recommendedDate: DateTimeOffset.UtcNow.AddDays(-30));
        var noRecommendationDate = CreateEntry();

        Assert.True(recentlyRecommended.IsNew(windowDays: 7));
        Assert.False(oldRecommendation.IsNew(windowDays: 7));
        Assert.False(noRecommendationDate.IsNew(windowDays: 7));
    }
}
