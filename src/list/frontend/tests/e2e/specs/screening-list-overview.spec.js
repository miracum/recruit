import { test, expect } from "@playwright/test";

// Keycloak is disabled for these tests (see tests/e2e/docker-compose.yaml), so the app
// always renders the admin view -- there's no separate regular-user view to assert on.
test.describe("ScreeningListOverview", () => {
  test.beforeEach(async ({ page }) => {
    const listResponse = page.waitForResponse(
      (response) => response.url().includes("/fhir/List") && response.request().method() === "GET"
    );
    await page.goto("/");
    await listResponse;
  });

  test("displays the PROSa, AMICA, and SECRET studies", async ({ page }) => {
    await expect(page.getByText("PROSa")).toBeVisible();
    await expect(page.getByText("AMICA")).toBeVisible();
    await expect(page.getByText("SECRET")).toBeVisible();
  });

  test("shows the active studies and, since Keycloak is disabled, the retired one too", async ({ page }) => {
    await expect(page.locator(".active-screening-lists .card")).toHaveCount(3);
    await expect(page.locator(".inactive-screening-lists .card")).toHaveCount(1);
    await expect(page.getByText("RETIRED")).toBeVisible();
  });

  test("shows toggles to change a list's status", async ({ page }) => {
    await expect(page.locator("label.switch")).toHaveCount(4);
  });

  test("moves a study between the active and inactive lists when its toggle is clicked", async ({ page }) => {
    await expect(page.locator(".active-screening-lists .card")).toHaveCount(3);
    await expect(page.locator(".inactive-screening-lists .card")).toHaveCount(1);

    await page.locator(".inactive-screening-lists .switch").click();
    await expect(page.locator(".active-screening-lists .card")).toHaveCount(4);
    await expect(page.locator(".inactive-screening-lists .card")).toHaveCount(0);

    // move it back so the test is repeatable and doesn't leave the fixture data mutated
    await page.locator(".active-screening-lists .card", { hasText: "RETIRED" }).locator(".switch").click();
    await expect(page.locator(".active-screening-lists .card")).toHaveCount(3);
    await expect(page.locator(".inactive-screening-lists .card")).toHaveCount(1);
  });
});
