import { test, expect } from "@playwright/test";

test.describe("Recommendations", () => {
  test.beforeEach(async ({ page }) => {
    // Navigate via the UI rather than a hard-coded list id, since the FHIR server
    // assigns ids at load time and we don't want the test coupled to that.
    const overviewListResponse = page.waitForResponse(
      (response) => response.url().includes("/fhir/List") && response.request().method() === "GET"
    );
    await page.goto("/");
    await overviewListResponse;

    // fhirclient renders this as "List/?_id=..." (slash before the query string).
    const recommendationsResponse = page.waitForResponse(
      (response) => response.url().includes("/fhir/List/?_id=") && response.request().method() === "GET"
    );
    await page.getByRole("link", { name: "AMICA" }).click();
    // <router-link> navigation is client-side; wait for the URL (and the h1.title, which
    // also exists as section headings on the overview page) to settle before asserting.
    await page.waitForURL(/\/recommendations\//);
    await recommendationsResponse;
  });

  test("displays the AMICA study as the page heading", async ({ page }) => {
    await expect(page.locator(".study-description-header h1.title")).toContainText("AMICA");
  });

  test("displays the total recommendations and participating studies counts", async ({ page }) => {
    const firstRow = page.locator("tbody tr").first();
    await expect(firstRow.locator("[data-label='Patientennummer'] .all-recommendations-count")).toHaveText("1");
    await expect(firstRow.locator("[data-label='Patientennummer'] .participating-studies-count")).toHaveText("1");
    await expect(firstRow.locator("[data-label='Patientennummer'] .is-no-longer-eligible")).toBeVisible();
  });

  test("can update and persist the recruitment status", async ({ page }) => {
    const firstRow = page.locator("tbody tr").first();

    await firstRow.locator("[data-label='Status'] .dropdown-trigger").click();
    // 4th option in the status dropdown is "on-study" / "Wurde eingeschlossen"
    await firstRow.locator("[data-label='Status'] .dropdown-item").nth(3).click();

    const patchResponse = page.waitForResponse(
      (response) => response.url().includes("/fhir/ResearchSubject/") && response.request().method() === "PATCH"
    );
    await firstRow.locator("[data-label='Aktionen'] .save-status").click();
    await patchResponse;

    await page.reload();
    const reloadedFirstRow = page.locator("tbody tr").first();
    await expect(reloadedFirstRow.locator("[data-label='Status'] .dropdown-trigger")).toContainText(
      "Wurde eingeschlossen"
    );
  });
});
