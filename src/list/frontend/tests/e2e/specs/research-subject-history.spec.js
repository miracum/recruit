import { test, expect } from "@playwright/test";

const patientMrNumber =
  "3354978E05D57044AA924A221928938F9C97179FD6D9E6E70CD199D4A31D9B21EFB5618A653B04DAA1E38D4FCE73BAD95DD26099C6C8F987F6332D8E21F6E48F";

test.describe("ResearchSubjectHistory", () => {
  test("displays the patient id and at least one history entry", async ({ page }) => {
    const overviewListResponse = page.waitForResponse(
      (response) => response.url().includes("/fhir/List") && response.request().method() === "GET"
    );
    await page.goto("/");
    await overviewListResponse;
    await page.getByRole("link", { name: "AMICA" }).click();
    // <router-link> navigation is client-side; wait for the URL (and the h1.title, which
    // also exists as section headings on the overview page) to settle before asserting.
    await page.waitForURL(/\/recommendations\//);
    await expect(page.locator(".study-description-header h1.title")).toContainText("AMICA");

    // "Änderungshistorie anzeigen" (history) button opens in a new tab.
    const historyLink = page.locator("a[href*='/history']").first();
    const [historyPage] = await Promise.all([page.waitForEvent("popup"), historyLink.click()]);
    await historyPage.waitForResponse(
      (response) => response.url().includes("/fhir/ResearchSubject/") && response.request().method() === "GET"
    );

    await expect(historyPage.locator("h1")).toContainText(patientMrNumber, { timeout: 30000 });
    await expect(historyPage.locator(".timeline .history-item").first()).toBeVisible();
  });
});
