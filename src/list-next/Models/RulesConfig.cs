namespace list.Models;

/// <summary>
/// Mirrors the notify-rules.yaml schema from the previous (list-old) implementation, so
/// existing deployment configuration can be reused unchanged.
/// </summary>
public sealed class RulesConfig
{
    public NotifyConfig Notify { get; set; } = new();
}

public sealed class NotifyConfig
{
    public RulesSection Rules { get; set; } = new();
}

public sealed class RulesSection
{
    public List<TrialRule> Trials { get; set; } = [];
}

public sealed class TrialRule
{
    public string Acronym { get; set; } = string.Empty;

    public List<TrialSubscription> Subscriptions { get; set; } = [];

    public TrialAccessibleBy? AccessibleBy { get; set; }
}

public sealed class TrialSubscription
{
    public string Email { get; set; } = string.Empty;
}

public sealed class TrialAccessibleBy
{
    public List<string> Users { get; set; } = [];
}
