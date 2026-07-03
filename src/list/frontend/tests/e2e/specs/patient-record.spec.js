import { test, expect } from "@playwright/test";

const patientMrNumber =
  "3354978E05D57044AA924A221928938F9C97179FD6D9E6E70CD199D4A31D9B21EFB5618A653B04DAA1E38D4FCE73BAD95DD26099C6C8F987F6332D8E21F6E48F";

// The "patient record" button opens in a new tab (target="_blank"), so each test opens it
// itself rather than sharing a beforeEach -- Playwright fixtures don't hand a popup page
// back from a hook, and it keeps each test's popup-handling explicit.
async function openPatientRecord(page) {
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

  const recordLink = page.locator("a[href*='/record']").first();
  const [recordPage] = await Promise.all([page.waitForEvent("popup"), recordLink.click()]);
  await recordPage.waitForResponse(
    (response) => response.url().includes("/fhir/Patient") && response.request().method() === "GET"
  );
  return recordPage;
}

test.describe("PatientRecord", () => {
  test("displays the patient's medical record number", async ({ page }) => {
    const recordPage = await openPatientRecord(page);
    await expect(recordPage.locator("h1")).toContainText(patientMrNumber, { timeout: 30000 });
  });

  test("shows the diagnoses, procedures, medication, and lab tabs", async ({ page }) => {
    const recordPage = await openPatientRecord(page);
    await expect(recordPage.getByRole("tab", { name: /Diagnosen/ })).toBeVisible();
    await expect(recordPage.getByRole("tab", { name: /Prozeduren/ })).toBeVisible();
    await expect(recordPage.getByRole("tab", { name: /Medikation/ })).toBeVisible();
    await expect(recordPage.getByRole("tab", { name: /Labor/ })).toBeVisible();
  });
});
