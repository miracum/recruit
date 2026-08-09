using System.Security.Claims;
using list.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace list.Services.Access;

/// <summary>
/// Enforces per-trial access control based on notify-rules.yaml, the same schema used by the
/// previous (list-old) Node proxy's fhirAccessFilter.js. The FHIR server itself has no concept
/// of "which studies can this user see" - that mapping only exists in this file, keyed by the
/// authenticated user's username/email.
/// </summary>
public sealed class TrialAccessService
{
    public const string AdminRole = "admin";
    private const string AllStudiesAcronym = "*";

    private readonly ILogger<TrialAccessService> _logger;
    private readonly List<TrialRule> _trials;

    public TrialAccessService(IConfiguration configuration, ILogger<TrialAccessService> logger)
    {
        _logger = logger;
        var rulesFilePath = configuration["RulesFilePath"] ?? "notify-rules.dev.yaml";
        _trials = LoadTrials(rulesFilePath, logger);
    }

    public IReadOnlyList<TrialRule> Trials => _trials;

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(AdminRole);

    /// <summary>Study acronyms the user may view. Contains "*" if the user can see everything.</summary>
    public IReadOnlyList<string> GetAccessibleStudyAcronyms(ClaimsPrincipal user)
    {
        if (IsAdmin(user))
        {
            return [AllStudiesAcronym];
        }

        var username = GetUsername(user);
        var email = GetEmail(user);

        if (username is null && email is null)
        {
            _logger.LogWarning("Neither username nor email are set for the current user. Denying all access.");
            return [];
        }

        var accessible = new List<string>();
        foreach (var trial in _trials)
        {
            var allowedByUser = trial.AccessibleBy?.Users is { } users &&
                                 ((username is not null && users.Contains(username)) ||
                                  (email is not null && users.Contains(email)));
            var allowedBySubscription = trial.Subscriptions.Any(s => s.Email == email);

            if (allowedByUser || allowedBySubscription)
            {
                accessible.Add(trial.Acronym);
            }
        }

        return accessible;
    }

    public bool CanAccessStudy(ClaimsPrincipal user, string? studyAcronym)
    {
        if (string.IsNullOrEmpty(studyAcronym))
        {
            return false;
        }

        var accessible = GetAccessibleStudyAcronyms(user);
        return accessible.Contains(AllStudiesAcronym) || accessible.Contains(studyAcronym);
    }

    /// <summary>ResearchSubject status/note PATCHes are allowed for anyone who can see the trial.</summary>
    public bool CanPatchResearchSubject(ClaimsPrincipal user, string studyAcronym) =>
        CanAccessStudy(user, studyAcronym);

    /// <summary>Only admins may retire/reactivate a List (matches list-old's createPatchFilter).</summary>
    public bool CanPatchList(ClaimsPrincipal user) => IsAdmin(user);

    /// <summary>Only admins may DELETE resources (matches list-old's createDeleteFilter).</summary>
    public bool CanDelete(ClaimsPrincipal user) => IsAdmin(user);

    private static string? GetUsername(ClaimsPrincipal user) =>
        user.FindFirst("preferred_username")?.Value
        ?? user.FindFirst(ClaimTypes.Name)?.Value
        ?? user.Identity?.Name;

    private static string? GetEmail(ClaimsPrincipal user) =>
        user.FindFirst("email")?.Value
        ?? user.FindFirst(ClaimTypes.Email)?.Value;

    private static List<TrialRule> LoadTrials(string rulesFilePath, ILogger logger)
    {
        try
        {
            logger.LogDebug("Loading trial access rules from {RulesFilePath}", rulesFilePath);
            using var reader = new StreamReader(rulesFilePath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<RulesConfig>(reader);
            var trials = config.Notify?.Rules?.Trials;
            if (trials is null)
            {
                throw new InvalidOperationException("Invalid configuration file structure. Expected notify.rules.trials.");
            }

            return trials;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load trial rules config from {RulesFilePath}. Defaulting to no access for any study.", rulesFilePath);
            return [];
        }
    }
}
