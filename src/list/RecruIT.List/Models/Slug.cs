using Slugify;

namespace RecruIT.List.Models;

/// <summary>
/// Lowercase, hyphen-separated slug derived from free text - e.g. "Patients 18 years or older"
/// becomes "patients-18-years-or-older". Idempotent: slugifying an already-valid slug is a no-op.
/// Thin wrapper around Slugify.Core (https://github.com/ctolkien/Slugify) so callers don't need to
/// depend on that package directly.
/// </summary>
public static class Slug
{
    private static readonly SlugHelper Helper = new();

    public static string Create(string value) => Helper.GenerateSlug(value);
}
