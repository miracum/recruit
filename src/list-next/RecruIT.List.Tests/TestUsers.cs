using System.Security.Claims;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Services.Access;

namespace RecruIT.List.Tests;

/// <summary>Shared ClaimsPrincipal/TrialAccessGrant test fixtures - see also SqliteDbContextFactory/FakeStringLocalizer.</summary>
internal static class TestUsers
{
    public static ClaimsPrincipal CreateUser(
        string? sub = null,
        string? email = null,
        string? name = null,
        bool isAdmin = false
    )
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

        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        if (isAdmin)
        {
            claims.Add(new Claim("role", TrialAccessService.AdminRole));
        }

        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "Test",
            nameType: "preferred_username",
            roleType: "role"
        );
        return new ClaimsPrincipal(identity);
    }

    public static async Task SeedGrantAsync(
        SqliteDbContextFactory factory,
        TrialIdentifier trial,
        string email,
        TrialPermissionLevel level,
        string? subjectId = null
    )
    {
        await using var db = factory.CreateDbContext();
        db.TrialAccessGrants.Add(
            new TrialAccessGrant
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
            }
        );
        await db.SaveChangesAsync();
    }
}
