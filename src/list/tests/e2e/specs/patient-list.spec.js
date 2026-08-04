import { test, expect } from "@playwright/test";

// Keycloak is disabled for these tests (see docker-compose.yaml), so the app always
// renders its unauthenticated view -- there's no login flow to exercise here.
test.describe("PatientList", () => {
  test.beforeEach(async ({ page }) => {
    const researchSubjectsResponse = page.waitForResponse(
      (response) => response.url().includes("/fhir/ResearchSubject") && response.request().method() === "GET"
    );
    await page.goto("/");
    await researchSubjectsResponse;
    // let the (virtualized) grid settle after the SignalR circuit takes over from prerendering.
    await page.waitForTimeout(1000);
  });

  const dataRows = (page) => page.locator(".mud-table-body tr").filter({ has: page.locator("button") });

  test("lists the ResearchSubjects loaded from the FHIR server, joined with their study", async ({ page }) => {
    await expect(dataRows(page)).toHaveCount(5);
    await expect(page.getByText("PROSa")).toBeVisible();
    await expect(page.getByText("AMICA")).toBeVisible();
  });

  test("narrows the list via the quick search box", async ({ page }) => {
    await page.getByPlaceholder("Search patient, study, note, status...").fill("withdrawn");
    await expect(dataRows(page)).toHaveCount(1);

    await page.getByPlaceholder("Search patient, study, note, status...").fill("");
    await expect(dataRows(page)).toHaveCount(5);
  });

  test("saves a status and note change back to the FHIR server", async ({ page }) => {
    const row = dataRows(page).first();
    await row.locator(".mud-select").first().click();
    await page.getByRole("option", { name: "on-study", exact: true }).click();
    await row.locator("input[type=text]").fill("reviewed via e2e test");
    await row.locator("button").click();

    await expect(page.locator(".mud-snackbar")).toContainText("Saved");

    await page.reload();
    await page.waitForResponse((response) => response.url().includes("/fhir/ResearchSubject") && response.request().method() === "GET");
    await page.waitForTimeout(1000);
    await expect(row.locator("input[type=text]")).toHaveValue("reviewed via e2e test");
  });
});
