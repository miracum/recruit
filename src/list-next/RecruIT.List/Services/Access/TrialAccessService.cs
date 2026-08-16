using System.Security.Claims;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace RecruIT.List.Services.Access;

/// <summary>
/// Enforces per-trial access control from the TrialAccessGrant table (Postgres), keyed by each
/// trial's ResearchStudy business identifier (see TrialIdentifier). A global "admin" OIDC role
/// bypasses every per-trial check. Grants are only ever looked up by the signed-in user's OIDC
/// "sub" (falling back to email for grants created before that user's first login - see
/// OidcEvents, which backfills SubjectId once they do log in).
/// </summary>
public sealed class TrialAccessService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IStringLocalizer<SharedResources> localizer,
    ILogger<TrialAccessService> logger
)
{
    public const string AdminRole = "admin";

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(AdminRole);

    /// <summary>
    /// The permission level to use as a fallback for admins - they outrank TrialAdmin without
    /// needing an actual grant row.
    /// </summary>
    private const TrialPermissionLevel AdminEffectiveLevel = TrialPermissionLevel.TrialAdmin;

    public async Task<TrialPermissionLevel?> GetPermissionAsync(
        ClaimsPrincipal user,
        TrialIdentifier trialIdentifier,
        CancellationToken ct = default
    )
    {
        if (IsAdmin(user))
        {
            return AdminEffectiveLevel;
        }

        var (subjectId, email) = GetUserKeys(user);
        if (subjectId is null && email is null)
        {
            logger.LogWarning(
                "Neither sub nor email are set for the current user. Denying all access."
            );
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var grant = await db
            .TrialAccessGrants.AsNoTracking()
            .FirstOrDefaultAsync(
                g =>
                    g.TrialIdentifierSystem == trialIdentifier.System
                    && g.TrialIdentifierValue == trialIdentifier.Value
                    && (
                        (subjectId != null && g.SubjectId == subjectId)
                        || (email != null && g.Email == email)
                    ),
                ct
            );

        return grant?.Level;
    }

    /// <summary>
    /// Every trial the user has any grant on, for scanning many trials at once (dashboard,
    /// notifications) without one DB round-trip per trial. Admin: null, meaning "all trials".
    /// </summary>
    public async Task<IReadOnlySet<TrialIdentifier>?> GetAccessibleTrialIdentifiersAsync(
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        if (IsAdmin(user))
        {
            return null;
        }

        var (subjectId, email) = GetUserKeys(user);
        if (subjectId is null && email is null)
        {
            logger.LogWarning(
                "Neither sub nor email are set for the current user. Denying all access."
            );
            return new HashSet<TrialIdentifier>();
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var grants = await db
            .TrialAccessGrants.AsNoTracking()
            .Where(g =>
                (subjectId != null && g.SubjectId == subjectId)
                || (email != null && g.Email == email)
            )
            .Select(g => new { g.TrialIdentifierSystem, g.TrialIdentifierValue })
            .ToListAsync(ct);

        return grants
            .Select(g => new TrialIdentifier(g.TrialIdentifierSystem, g.TrialIdentifierValue))
            .ToHashSet();
    }

    /// <summary>Companion to GetAccessibleTrialIdentifiersAsync - null means "admin, everything is accessible".</summary>
    public static bool CanAccessTrial(
        IReadOnlySet<TrialIdentifier>? accessibleTrials,
        TrialIdentifier? trialIdentifier
    ) =>
        trialIdentifier is not null
        && (accessibleTrials is null || accessibleTrials.Contains(trialIdentifier));

    public async Task<bool> CanAccessTrialAsync(
        ClaimsPrincipal user,
        TrialIdentifier? trialIdentifier,
        CancellationToken ct = default
    ) =>
        trialIdentifier is not null
        && await GetPermissionAsync(user, trialIdentifier, ct) is not null;

    /// <summary>ResearchSubject status/note PATCHes require Coordinator or above.</summary>
    public async Task<bool> CanPatchResearchSubjectAsync(
        ClaimsPrincipal user,
        TrialIdentifier trialIdentifier,
        CancellationToken ct = default
    ) =>
        await GetPermissionAsync(user, trialIdentifier, ct) is { } level
        && level >= TrialPermissionLevel.Coordinator;

    /// <summary>Managing who else has access to a trial requires TrialAdmin (or global Admin).</summary>
    public async Task<bool> CanManageAccessAsync(
        ClaimsPrincipal user,
        TrialIdentifier trialIdentifier,
        CancellationToken ct = default
    ) =>
        await GetPermissionAsync(user, trialIdentifier, ct) is { } level
        && level >= TrialPermissionLevel.TrialAdmin;

    /// <summary>Only admins may retire/reactivate a List (matches list-old's createPatchFilter).</summary>
    public bool CanPatchList(ClaimsPrincipal user) => IsAdmin(user);

    /// <summary>Only admins may DELETE resources (matches list-old's createDeleteFilter).</summary>
    public bool CanDelete(ClaimsPrincipal user) => IsAdmin(user);

    /// <summary>
    /// Whether this grant belongs to the given user - used to stop a TrialAdmin from editing or
    /// revoking their own grant and accidentally locking themselves out of managing this trial.
    /// </summary>
    public static bool IsOwnGrant(TrialAccessGrant grant, ClaimsPrincipal user)
    {
        var (subjectId, email) = GetUserKeys(user);
        return (subjectId is not null && grant.SubjectId == subjectId)
            || (email is not null && grant.Email == email);
    }

    public async Task<IReadOnlyList<TrialAccessGrant>> ListGrantsAsync(
        TrialIdentifier trialIdentifier,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        await EnsureCanManageAccessAsync(user, trialIdentifier, ct);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db
            .TrialAccessGrants.AsNoTracking()
            .Where(g =>
                g.TrialIdentifierSystem == trialIdentifier.System
                && g.TrialIdentifierValue == trialIdentifier.Value
            )
            .OrderBy(g => g.Email)
            .ToListAsync(ct);
    }

    public async Task AddOrUpdateGrantAsync(
        TrialIdentifier trialIdentifier,
        string email,
        TrialPermissionLevel level,
        ClaimsPrincipal grantedBy,
        CancellationToken ct = default
    )
    {
        await EnsureCanManageAccessAsync(grantedBy, trialIdentifier, ct);

        var normalizedEmail = email.Trim();
        var (granterSubjectId, granterEmail) = GetUserKeys(grantedBy);
        var granterIdentity = granterSubjectId ?? granterEmail ?? "unknown";

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.TrialAccessGrants.FirstOrDefaultAsync(
            g =>
                g.TrialIdentifierSystem == trialIdentifier.System
                && g.TrialIdentifierValue == trialIdentifier.Value
                && g.Email == normalizedEmail,
            ct
        );

        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            db.TrialAccessGrants.Add(
                new TrialAccessGrant
                {
                    Id = Guid.NewGuid(),
                    TrialIdentifierSystem = trialIdentifier.System,
                    TrialIdentifierValue = trialIdentifier.Value,
                    Email = normalizedEmail,
                    Level = level,
                    GrantedBy = granterIdentity,
                    GrantedAt = now,
                    UpdatedAt = now,
                }
            );
        }
        else
        {
            // A TrialAdmin (or anyone) editing their own existing grant could accidentally
            // demote or otherwise change their own access - reject it outright, matching the
            // disabled control on the access-management page.
            if (IsOwnGrant(existing, grantedBy))
            {
                throw new UnauthorizedAccessException(localizer["App.Errors.CannotModifyOwnGrant"]);
            }

            existing.Level = level;
            existing.GrantedBy = granterIdentity;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeGrantAsync(
        Guid grantId,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var grant = await db.TrialAccessGrants.FirstOrDefaultAsync(g => g.Id == grantId, ct);
        if (grant is null)
        {
            return;
        }

        await EnsureCanManageAccessAsync(
            user,
            new TrialIdentifier(grant.TrialIdentifierSystem, grant.TrialIdentifierValue),
            ct
        );

        if (IsOwnGrant(grant, user))
        {
            throw new UnauthorizedAccessException(localizer["App.Errors.CannotModifyOwnGrant"]);
        }

        db.TrialAccessGrants.Remove(grant);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCanManageAccessAsync(
        ClaimsPrincipal user,
        TrialIdentifier trialIdentifier,
        CancellationToken ct
    )
    {
        if (!await CanManageAccessAsync(user, trialIdentifier, ct))
        {
            throw new UnauthorizedAccessException(
                localizer["App.Errors.NotAuthorizedManageAccess"]
            );
        }
    }

    private static (string? SubjectId, string? Email) GetUserKeys(ClaimsPrincipal user) =>
        (
            user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value
        );
}
