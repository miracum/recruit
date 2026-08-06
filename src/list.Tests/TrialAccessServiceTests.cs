using System.Security.Claims;
using list.Services.Access;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace list.Tests;

public sealed class TrialAccessServiceTests : IDisposable
{
    private const string RulesYaml = """
        notify:
          rules:
            trials:
              - acronym: "*"
                subscriptions:
                  - email: "wildcard.user@example.com"
              - acronym: "PROSa"
                subscriptions: []
                accessibleBy:
                  users:
                    - "user1"
              - acronym: "AMICA"
                subscriptions:
                  - email: "user.one@example.com"
        """;

    private readonly string _rulesFilePath;
    private readonly TrialAccessService _service;

    public TrialAccessServiceTests()
    {
        _rulesFilePath = Path.GetTempFileName();
        File.WriteAllText(_rulesFilePath, RulesYaml);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RulesFilePath"] = _rulesFilePath })
            .Build();

        _service = new TrialAccessService(configuration, NullLogger<TrialAccessService>.Instance);
    }

    public void Dispose() => File.Delete(_rulesFilePath);

    private static ClaimsPrincipal CreateUser(string? username = null, string? email = null, bool admin = false)
    {
        var claims = new List<Claim>();
        if (username is not null)
        {
            claims.Add(new Claim("preferred_username", username));
        }

        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        if (admin)
        {
            claims.Add(new Claim("role", TrialAccessService.AdminRole));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "preferred_username", "role"));
    }

    [Fact]
    public void Admin_can_access_any_study()
    {
        var admin = CreateUser(admin: true);

        Assert.True(_service.CanAccessStudy(admin, "PROSa"));
        Assert.True(_service.CanAccessStudy(admin, "SomeOtherStudyNotInRules"));
    }

    [Fact]
    public void User_listed_in_accessibleBy_can_access_that_study_only()
    {
        var user = CreateUser(username: "user1");

        Assert.True(_service.CanAccessStudy(user, "PROSa"));
        Assert.False(_service.CanAccessStudy(user, "AMICA"));
    }

    [Fact]
    public void User_with_matching_subscription_email_can_access_that_study()
    {
        var user = CreateUser(username: "someone-else", email: "user.one@example.com");

        Assert.True(_service.CanAccessStudy(user, "AMICA"));
    }

    [Fact]
    public void Wildcard_trial_subscription_grants_access_to_every_study()
    {
        var user = CreateUser(username: "wildcard-guy", email: "wildcard.user@example.com");

        Assert.True(_service.CanAccessStudy(user, "PROSa"));
        Assert.True(_service.CanAccessStudy(user, "AnythingAtAll"));
    }

    [Fact]
    public void User_without_any_matching_rule_is_denied()
    {
        var user = CreateUser(username: "nobody", email: "nobody@example.com");

        Assert.False(_service.CanAccessStudy(user, "PROSa"));
        Assert.False(_service.CanAccessStudy(user, "AMICA"));
    }

    [Fact]
    public void User_without_username_or_email_is_denied_everything()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Empty(_service.GetAccessibleStudyAcronyms(user));
    }

    [Fact]
    public void Only_admins_can_patch_or_delete_lists()
    {
        var regularUser = CreateUser(username: "user1");
        var admin = CreateUser(admin: true);

        Assert.False(_service.CanPatchList(regularUser));
        Assert.False(_service.CanDelete(regularUser));
        Assert.True(_service.CanPatchList(admin));
        Assert.True(_service.CanDelete(admin));
    }

    [Fact]
    public void Any_user_with_study_access_can_patch_a_research_subject_for_that_study()
    {
        var user = CreateUser(username: "user1");

        Assert.True(_service.CanPatchResearchSubject(user, "PROSa"));
        Assert.False(_service.CanPatchResearchSubject(user, "AMICA"));
    }
}
