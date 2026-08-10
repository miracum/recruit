using System.Security.Claims;
using list.Data.Entities;
using list.Models;
using list.Services.Access;
using Microsoft.Extensions.Logging.Abstractions;

namespace list.Tests;

public sealed class TrialAccessServiceTests
{
    private static readonly TrialIdentifier TrialA = new("https://fhir.example.org/study-id", "STUDY-A");
    private static readonly TrialIdentifier TrialB = new("https://fhir.example.org/study-id", "STUDY-B");

    private static ClaimsPrincipal CreateUser(string? sub = null, string? email = null, bool isAdmin = false)
    {
        var claims = new List<Claim>();
        if (sub is not null)
        {
            claims.Add(new Claim("sub", sub));
        }

        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        if (isAdmin)
        {
            claims.Add(new Claim("role", TrialAccessService.AdminRole));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "Test", nameType: "preferred_username", roleType: "role");
        return new ClaimsPrincipal(identity);
    }

    private static TrialAccessService CreateService(SqliteDbContextFactory factory) =>
        new(factory, new FakeStringLocalizer(), NullLogger<TrialAccessService>.Instance);

    private static async Task SeedGrantAsync(
        SqliteDbContextFactory factory, TrialIdentifier trial, string email, TrialPermissionLevel level, string? subjectId = null)
    {
        await using var db = factory.CreateDbContext();
        db.TrialAccessGrants.Add(new TrialAccessGrant
        {
            Id = Guid.NewGuid(),
            TrialIdentifierSystem = trial.System,
            TrialIdentifierValue = trial.Value,
            Email = email,
            SubjectId = subjectId,
            Level = level,
            GrantedBy = "test",
            GrantedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Admin_bypasses_all_per_trial_checks_without_any_grant()
    {
        using var factory = new SqliteDbContextFactory();
        var service = CreateService(factory);
        var admin = CreateUser(sub: "admin-1", isAdmin: true);

        Assert.True(await service.CanAccessTrialAsync(admin, TrialA));
        Assert.True(await service.CanPatchResearchSubjectAsync(admin, TrialA));
        Assert.True(await service.CanManageAccessAsync(admin, TrialA));
        Assert.Null(await service.GetAccessibleTrialIdentifiersAsync(admin));
    }

    [Fact]
    public async Task User_with_no_grant_has_no_access()
    {
        using var factory = new SqliteDbContextFactory();
        var service = CreateService(factory);
        var user = CreateUser(sub: "user-1", email: "user1@example.com");

        Assert.False(await service.CanAccessTrialAsync(user, TrialA));
        Assert.False(await service.CanPatchResearchSubjectAsync(user, TrialA));
        Assert.False(await service.CanManageAccessAsync(user, TrialA));
        Assert.Null(await service.GetPermissionAsync(user, TrialA));
    }

    [Fact]
    public async Task Viewer_can_access_but_cannot_patch_or_manage_access()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "viewer@example.com", TrialPermissionLevel.Viewer, subjectId: "viewer-1");
        var service = CreateService(factory);
        var viewer = CreateUser(sub: "viewer-1", email: "viewer@example.com");

        Assert.True(await service.CanAccessTrialAsync(viewer, TrialA));
        Assert.False(await service.CanPatchResearchSubjectAsync(viewer, TrialA));
        Assert.False(await service.CanManageAccessAsync(viewer, TrialA));
    }

    [Fact]
    public async Task Coordinator_can_patch_but_cannot_manage_access()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "coord@example.com", TrialPermissionLevel.Coordinator, subjectId: "coord-1");
        var service = CreateService(factory);
        var coordinator = CreateUser(sub: "coord-1", email: "coord@example.com");

        Assert.True(await service.CanAccessTrialAsync(coordinator, TrialA));
        Assert.True(await service.CanPatchResearchSubjectAsync(coordinator, TrialA));
        Assert.False(await service.CanManageAccessAsync(coordinator, TrialA));
    }

    [Fact]
    public async Task TrialAdmin_can_manage_access()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "trialadmin@example.com", TrialPermissionLevel.TrialAdmin, subjectId: "ta-1");
        var service = CreateService(factory);
        var trialAdmin = CreateUser(sub: "ta-1", email: "trialadmin@example.com");

        Assert.True(await service.CanManageAccessAsync(trialAdmin, TrialA));
    }

    [Fact]
    public async Task Grant_created_by_email_before_first_login_is_honored_immediately()
    {
        // Simulates an admin inviting someone by email before that person has ever signed in -
        // SubjectId is null until OidcEvents backfills it on their first login.
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "invitee@example.com", TrialPermissionLevel.Coordinator, subjectId: null);
        var service = CreateService(factory);
        var invitee = CreateUser(sub: "not-yet-linked-sub", email: "invitee@example.com");

        Assert.True(await service.CanPatchResearchSubjectAsync(invitee, TrialA));
    }

    [Fact]
    public async Task Grant_on_one_trial_does_not_leak_access_to_a_different_trial_with_the_same_system()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "user@example.com", TrialPermissionLevel.TrialAdmin, subjectId: "user-1");
        var service = CreateService(factory);
        var user = CreateUser(sub: "user-1", email: "user@example.com");

        Assert.True(await service.CanAccessTrialAsync(user, TrialA));
        Assert.False(await service.CanAccessTrialAsync(user, TrialB));
    }

    [Fact]
    public async Task GetAccessibleTrialIdentifiersAsync_returns_exactly_the_users_granted_trials()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "user@example.com", TrialPermissionLevel.Viewer, subjectId: "user-1");
        var service = CreateService(factory);
        var user = CreateUser(sub: "user-1", email: "user@example.com");

        var accessible = await service.GetAccessibleTrialIdentifiersAsync(user);

        Assert.NotNull(accessible);
        Assert.Single(accessible);
        Assert.Contains(TrialA, accessible);
        Assert.DoesNotContain(TrialB, accessible);
    }

    [Fact]
    public async Task AddOrUpdateGrantAsync_by_a_non_manager_throws()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "viewer@example.com", TrialPermissionLevel.Viewer, subjectId: "viewer-1");
        var service = CreateService(factory);
        var viewer = CreateUser(sub: "viewer-1", email: "viewer@example.com");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddOrUpdateGrantAsync(TrialA, "someone-else@example.com", TrialPermissionLevel.Viewer, viewer));
    }

    [Fact]
    public async Task TrialAdmin_cannot_change_their_own_level()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "trialadmin@example.com", TrialPermissionLevel.TrialAdmin, subjectId: "ta-1");
        var service = CreateService(factory);
        var trialAdmin = CreateUser(sub: "ta-1", email: "trialadmin@example.com");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddOrUpdateGrantAsync(TrialA, "trialadmin@example.com", TrialPermissionLevel.Coordinator, trialAdmin));

        Assert.Equal(TrialPermissionLevel.TrialAdmin, await service.GetPermissionAsync(trialAdmin, TrialA));
    }

    [Fact]
    public async Task TrialAdmin_cannot_revoke_their_own_grant()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "trialadmin@example.com", TrialPermissionLevel.TrialAdmin, subjectId: "ta-1");
        var service = CreateService(factory);
        var trialAdmin = CreateUser(sub: "ta-1", email: "trialadmin@example.com");

        var ownGrant = Assert.Single(await service.ListGrantsAsync(TrialA, trialAdmin));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RevokeGrantAsync(ownGrant.Id, trialAdmin));

        Assert.True(await service.CanManageAccessAsync(trialAdmin, TrialA));
    }

    [Fact]
    public async Task Admin_cannot_modify_their_own_grant_either()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "admin@example.com", TrialPermissionLevel.Viewer, subjectId: "admin-1");
        var service = CreateService(factory);
        var admin = CreateUser(sub: "admin-1", email: "admin@example.com", isAdmin: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddOrUpdateGrantAsync(TrialA, "admin@example.com", TrialPermissionLevel.TrialAdmin, admin));
    }

    [Fact]
    public async Task TrialAdmin_can_grant_and_revoke_access_for_their_trial()
    {
        using var factory = new SqliteDbContextFactory();
        await SeedGrantAsync(factory, TrialA, "trialadmin@example.com", TrialPermissionLevel.TrialAdmin, subjectId: "ta-1");
        var service = CreateService(factory);
        var trialAdmin = CreateUser(sub: "ta-1", email: "trialadmin@example.com");
        var newUser = CreateUser(sub: "new-1", email: "new@example.com");

        await service.AddOrUpdateGrantAsync(TrialA, "new@example.com", TrialPermissionLevel.Coordinator, trialAdmin);
        Assert.True(await service.CanPatchResearchSubjectAsync(newUser, TrialA));

        var grants = await service.ListGrantsAsync(TrialA, trialAdmin);
        var newGrant = Assert.Single(grants, g => g.Email == "new@example.com");

        await service.RevokeGrantAsync(newGrant.Id, trialAdmin);

        Assert.False(await service.CanAccessTrialAsync(newUser, TrialA));
    }
}
