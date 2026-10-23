using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;

namespace RecruIT.List.E2ETests;

/// <summary>
/// The in-app notification flow end to end, driven by the Notification Test page's re-recommend
/// action - which goes through the real FHIR List history and detector, so this also covers a
/// patient being removed and re-added between two detector runs. Picks the first trial and
/// patient rather than the seeded ones by name, so it also works against an E2E_BASE_URL instance.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class NotificationTests(AppFixture app) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    [Fact]
    public async Task Re_recommending_a_patient_notifies_a_subscriber_until_they_open_it()
    {
        var (trial, patient) = await SelectFirstTrialAndPatientAsync();
        var notSubscribed = Page.GetByText("You aren't subscribed to this trial");
        if (await notSubscribed.IsVisibleAsync())
        {
            await Page.GetByRole(AriaRole.Link, new() { Name = "Open screening list" })
                .ClickAsync();
            await SubscribeInstantlyAsync();
            (trial, patient) = await SelectFirstTrialAndPatientAsync();
        }

        await Page.GetByRole(AriaRole.Button, new() { Name = "Re-recommend patient" }).ClickAsync();
        await Expect(Page.GetByText("Notification created")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = "Open notifications" }).ClickAsync();
        var notification = Page.GetByText($"{patient} was newly recommended for {trial}");
        await Expect(notification.First).ToBeVisibleAsync();

        // The screening list marks the patient as new while their notification is unread...
        await Page.GetByRole(AriaRole.Link, new() { Name = trial, Exact = true }).ClickAsync();
        var row = Page.Locator("tr", new() { HasText = patient });
        await Expect(row.GetByTitle("New suggestion")).ToBeVisibleAsync();

        // ...and opening them marks it read.
        await row.GetByText(patient, new() { Exact = true }).ClickAsync();
        await Expect(row.GetByTitle("New suggestion")).ToHaveCountAsync(0);

        await Page.GotoAsync($"{app.BaseUrl}/notifications");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Unread" })).ToBeVisibleAsync();
        await Expect(notification).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Opens the Notification Test page and picks its first trial and patient, returning the
    /// trial's acronym and the patient's display name as the page shows them.
    /// </summary>
    private async Task<(string Trial, string Patient)> SelectFirstTrialAndPatientAsync()
    {
        if (!Page.Url.EndsWith("/admin/notify-test", StringComparison.Ordinal))
        {
            await Page.GotoAsync($"{app.BaseUrl}/admin/notify-test");
        }

        var trial = await PickFirstOptionAsync("Trial");
        // Shown once the chosen trial's patients and subscriptions have loaded - which is also
        // when the "not subscribed" warning appears, if it does.
        await Expect(Page.GetByText("will be notified")).ToBeVisibleAsync();
        var patient = await PickFirstOptionAsync("Patient");
        return (trial, patient);
    }

    private async Task<string> PickFirstOptionAsync(string selectName)
    {
        var combobox = Page.GetByRole(AriaRole.Combobox, new() { Name = selectName });
        await OpenUntilVisibleAsync(combobox, combobox.And(Page.Locator("[aria-expanded='true']")));
        // Scoped to this select's own listbox: the previous select's may still be closing.
        var listboxId = await combobox.GetAttributeAsync("aria-controls");
        var option = Page.Locator($"[id='{listboxId}']").GetByRole(AriaRole.Option).First;
        var text = (await option.InnerTextAsync()).Trim();
        await option.ClickAsync();
        return text;
    }

    /// <summary>
    /// Pages are prerendered, so a click that lands before Blazor's circuit has connected does
    /// nothing - retried until it actually opens what it should.
    /// </summary>
    private static async Task OpenUntilVisibleAsync(ILocator trigger, ILocator opened)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await trigger.ClickAsync();
            try
            {
                await opened.WaitForAsync(new() { Timeout = 1500 });
                return;
            }
            catch (TimeoutException) { }
        }

        throw new TimeoutException("Clicking never opened the expected element.");
    }

    /// <summary>On a trial's screening list: subscribes with the default (Instant) frequency.</summary>
    private async Task SubscribeInstantlyAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Notification settings" })
            .ClickAsync();
        var toggle = Page.GetByRole(AriaRole.Switch, new() { Name = "Notification settings" });
        if (!await toggle.IsCheckedAsync())
        {
            await toggle.ClickAsync();
        }

        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Notification preferences saved.")).ToBeVisibleAsync();
    }
}
