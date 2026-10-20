using Microsoft.Extensions.Localization;
using RecruIT.List.Resources;

namespace RecruIT.List.Tests;

/// <summary>Echoes the key back instead of loading resx data - these tests exercise access logic, not translations.</summary>
internal sealed class FakeStringLocalizer : IStringLocalizer<SharedResources>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
