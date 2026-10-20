using System.Reflection;
using RecruIT.List.Services.Notify;

namespace RecruIT.List.Tests;

public class NotificationDigestTemplateTests
{
    [Fact]
    public void RenderHtml_RendersAggregateCountWithoutAnyPerPatientData()
    {
        var html = InvokeRenderHtml(3, "<b>ACME</b>", "https://example.org/list?id=123&x=y");

        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Es wurden 3 mögliche neue Rekrutierungsvorschläge",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("&lt;b&gt;ACME&lt;/b&gt;-Studie", html, StringComparison.Ordinal);
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

    private static string InvokeRenderHtml(
        int numNewSubjects,
        string studyAcronym,
        string screeningListLink
    )
    {
        var method = typeof(NotificationSenderService).GetMethod(
            "RenderHtml",
            BindingFlags.NonPublic | BindingFlags.Static
        )!;
        return (string)method.Invoke(null, [numNewSubjects, studyAcronym, screeningListLink])!;
    }
}
