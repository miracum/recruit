using BlazorBlueprint.Components;
using Microsoft.Extensions.Localization;
using RecruIT.List.Resources;

namespace RecruIT.List.Services.Localization;

/// <summary>
/// Routes BlazorBlueprintUI's built-in component strings (DataTable, DataView, Pagination, ...)
/// through our own SharedResources.resx/.de.resx instead of the library's baked-in English
/// defaults, per https://blazorblueprintui.com/docs/localization. Falls back to the library's
/// English default when a key has no translation for the current culture.
/// </summary>
public sealed class AppBbLocalizer(IStringLocalizer<SharedResources> localizer) : DefaultBbLocalizer
{
    public override string this[string key] =>
        localizer[key] is { ResourceNotFound: false } found ? found.Value : base[key];

    public override string this[string key, params object[] arguments] =>
        localizer[key, arguments] is { ResourceNotFound: false } found
            ? found.Value
            : base[key, arguments];
}
