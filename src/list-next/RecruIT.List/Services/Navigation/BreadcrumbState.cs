namespace RecruIT.List.Services.Navigation;

public sealed record BreadcrumbItem(string Text, string? Href = null);

/// <summary>
/// Lets pages publish their breadcrumb trail (e.g. "All trials > AMICA") for MainLayout's header
/// to render as the page title, since the trail often depends on data only the page has loaded
/// (a trial's acronym, a patient's name).
/// </summary>
public sealed class BreadcrumbState
{
    public IReadOnlyList<BreadcrumbItem> Items { get; private set; } = [];

    public event Action? Changed;

    public void Set(params BreadcrumbItem[] items)
    {
        Items = items;
        Changed?.Invoke();
    }
}
