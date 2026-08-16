namespace RecruIT.List.Models;

/// <summary>
/// Per-trial permission levels, ordered so higher levels satisfy lower-level checks
/// (Level &gt;= Coordinator). A global Admin (OIDC role, unrelated to this enum) bypasses
/// per-trial grants entirely and is treated as outranking TrialAdmin wherever a level is needed.
/// </summary>
public enum TrialPermissionLevel
{
    Viewer = 0,
    Coordinator = 1,
    TrialAdmin = 2,
}
