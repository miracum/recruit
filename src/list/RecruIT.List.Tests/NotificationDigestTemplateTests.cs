using System.Reflection;
using RecruIT.List.Services.Notify;

namespace RecruIT.List.Tests;

public class NotificationDigestTemplateTests
{
    [Fact]
    public void RenderHtml_RendersAggregateCountWithoutAnyPerPatientData()
    {
        var html = InvokeRenderHtml(3, ["<b>ACME</b>"], "https://example.org/list?id=123&x=y");

        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3 neue Rekrutierungsvorschläge", html, StringComparison.Ordinal);
        Assert.Contains("mögliche neue Vorschläge", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;ACME&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.Contains("Screeningliste öffnen", html, StringComparison.Ordinal);
        Assert.Contains(
            "href=\"https://example.org/list?id=123&amp;x=y\"",
            html,
            StringComparison.Ordinal
        );

        // Data protection: the digest must never carry patient-identifying strings - the
        // renderer is only ever given an aggregate count, so this is really asserting the
        // template itself has no leftover per-patient placeholder.
        Assert.DoesNotContain("Patient", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderHtml_UsesSingularForOneSuggestion()
    {
        var html = InvokeRenderHtml(1, ["ACME"], "https://example.org/");

        Assert.Contains("1 neuer Rekrutierungsvorschlag", html, StringComparison.Ordinal);
        Assert.Contains("möglicher neuer Vorschlag", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHtml_ListsEveryTrialAndLinksToOverviewForMultiTrialDigest()
    {
        var html = InvokeRenderHtml(5, ["ACME", "BETA"], "https://example.org/");

        Assert.Contains(">ACME</span>", html, StringComparison.Ordinal);
        Assert.Contains(">BETA</span>", html, StringComparison.Ordinal);
        Assert.Contains("Ihre abonnierten Studien", html, StringComparison.Ordinal);
        Assert.Contains("Zur Studienübersicht", html, StringComparison.Ordinal);
        Assert.DoesNotContain("trials", html, StringComparison.Ordinal);
    }

    private static string InvokeRenderHtml(
        int numNewSubjects,
        IReadOnlyList<string> studyAcronyms,
        string screeningListLink
    )
    {
        var method = typeof(NotificationSenderService).GetMethod(
            "RenderHtml",
            BindingFlags.NonPublic | BindingFlags.Static
        )!;
        return (string)method.Invoke(null, [numNewSubjects, studyAcronyms, screeningListLink])!;
    }
}
