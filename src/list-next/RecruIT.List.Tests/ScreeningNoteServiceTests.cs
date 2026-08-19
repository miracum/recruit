using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Services;
using RecruIT.List.Services.Access;

namespace RecruIT.List.Tests;

[Collection(nameof(TrialAccessRoleCollection))]
public sealed class ScreeningNoteServiceTests
{
    private static readonly TrialIdentifier TrialA = new(
        "https://fhir.example.org/study-id",
        "STUDY-A"
    );

    private const string SubjectAIdentifier =
        "https://fhir.example.org/research-subject-id|subject-a";
    private const string SubjectBIdentifier =
        "https://fhir.example.org/research-subject-id|subject-b";

    private static ClaimsPrincipal CreateUser(
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

    private static (ScreeningNoteService Notes, SqliteDbContextFactory Factory) CreateService()
    {
        var factory = new SqliteDbContextFactory();
        var accessService = new TrialAccessService(
            factory,
            new FakeStringLocalizer(),
            NullLogger<TrialAccessService>.Instance
        );
        var notes = new ScreeningNoteService(
            factory,
            accessService,
            new FakeStringLocalizer(),
            NullLogger<ScreeningNoteService>.Instance
        );
        return (notes, factory);
    }

    private static async Task SeedGrantAsync(
        SqliteDbContextFactory factory,
        TrialIdentifier trial,
        string email,
        TrialPermissionLevel level,
        string subjectId
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

    [Fact]
    public async Task AddNoteAsync_persists_a_note_readable_via_GetNotesAsync()
    {
        var (notes, factory) = CreateService();
        using var _ = factory;
        await SeedGrantAsync(
            factory,
            TrialA,
            "coord@example.com",
            TrialPermissionLevel.Coordinator,
            "coord-1"
        );
        var user = CreateUser(sub: "coord-1", email: "coord@example.com", name: "Coord Person");

        await notes.AddNoteAsync(SubjectAIdentifier, "Patient looks eligible.", TrialA, user);

        var result = await notes.GetNotesAsync(SubjectAIdentifier);

        var note = Assert.Single(result);
        Assert.Equal("Patient looks eligible.", note.Text);
        Assert.Equal("Coord Person", note.Author);
        Assert.NotNull(note.Time);
    }

    [Fact]
    public async Task GetNotesAsync_returns_notes_newest_first()
    {
        var (notes, factory) = CreateService();
        using var _ = factory;
        await SeedGrantAsync(
            factory,
            TrialA,
            "coord@example.com",
            TrialPermissionLevel.Coordinator,
            "coord-1"
        );
        var user = CreateUser(sub: "coord-1", email: "coord@example.com");

        await notes.AddNoteAsync(SubjectAIdentifier, "first", TrialA, user);
        await notes.AddNoteAsync(SubjectAIdentifier, "second", TrialA, user);
        await notes.AddNoteAsync(SubjectAIdentifier, "third", TrialA, user);

        var result = await notes.GetNotesAsync(SubjectAIdentifier);

        Assert.Equal(["third", "second", "first"], result.Select(n => n.Text));
    }

    [Fact]
    public async Task GetNotesAsync_is_isolated_per_research_subject()
    {
        var (notes, factory) = CreateService();
        using var _ = factory;
        await SeedGrantAsync(
            factory,
            TrialA,
            "coord@example.com",
            TrialPermissionLevel.Coordinator,
            "coord-1"
        );
        var user = CreateUser(sub: "coord-1", email: "coord@example.com");

        await notes.AddNoteAsync(SubjectAIdentifier, "for subject A", TrialA, user);
        await notes.AddNoteAsync(SubjectBIdentifier, "for subject B", TrialA, user);

        var subjectANotes = await notes.GetNotesAsync(SubjectAIdentifier);
        var subjectBNotes = await notes.GetNotesAsync(SubjectBIdentifier);

        Assert.Equal("for subject A", Assert.Single(subjectANotes).Text);
        Assert.Equal("for subject B", Assert.Single(subjectBNotes).Text);
    }

    [Fact]
    public async Task AddNoteAsync_by_a_user_without_patch_access_throws_UnauthorizedAccessException()
    {
        var (notes, factory) = CreateService();
        using var _ = factory;
        await SeedGrantAsync(
            factory,
            TrialA,
            "viewer@example.com",
            TrialPermissionLevel.Viewer,
            "viewer-1"
        );
        var viewer = CreateUser(sub: "viewer-1", email: "viewer@example.com");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            notes.AddNoteAsync(SubjectAIdentifier, "not allowed", TrialA, viewer)
        );

        Assert.Empty(await notes.GetNotesAsync(SubjectAIdentifier));
    }

    [Fact]
    public async Task AddNoteAsync_falls_back_to_email_when_display_name_claim_is_absent()
    {
        var (notes, factory) = CreateService();
        using var _ = factory;
        await SeedGrantAsync(
            factory,
            TrialA,
            "coord@example.com",
            TrialPermissionLevel.Coordinator,
            "coord-1"
        );
        // GetAuthorDisplayName's fallback (no "name" claim, no NameClaimType identity name) reads
        // ClaimTypes.Email specifically, not the short-form "email" claim CreateUser adds - so the
        // claim is built directly here rather than via CreateUser.
        var identity = new ClaimsIdentity(
            [new Claim("sub", "coord-1"), new Claim(ClaimTypes.Email, "coord@example.com")],
            authenticationType: "Test"
        );
        var user = new ClaimsPrincipal(identity);

        await notes.AddNoteAsync(SubjectAIdentifier, "note text", TrialA, user);

        var note = Assert.Single(await notes.GetNotesAsync(SubjectAIdentifier));
        Assert.Equal("coord@example.com", note.Author);
    }
}
