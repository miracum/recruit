// https://docs.cypress.io/api/introduction/api.html
import { oidcConfig } from "../fixtures/keycloak";

const listRequestUrl = "**/List/?_id=**";

describe("Recommendations ", () => {
  beforeEach(() => {
    cy.login(oidcConfig);
  });
  context("after loading recommendations", () => {
    beforeEach(() => {
      cy.intercept({
        method: "GET",
        path: listRequestUrl,
      }).as("getList");

      cy.intercept({
        method: "PATCH",
        path: "**/ResearchSubject/**",
      }).as("patchSubject");

      cy.visit("/recommendations/796", {
        onBeforeLoad: (win) => {
          // eslint-disable-next-line no-param-reassign
          win.fetch = null;
        },
      });
      cy.wait("@getList", { timeout: 30000 });
    });

    it("displays AMICA sample study", () => {
      cy.get("h1.title").contains("AMICA");
    });

    it("displays correct total recommendations and participating studies count", () => {
      cy.get(":nth-child(1) > [data-label='Patientennummer'] .all-recommendations-count").should("contain.text", "1");
      cy.get(":nth-child(1) > [data-label='Patientennummer'] .participating-studies-count").should("contain.text", "1");
      cy.get(":nth-child(1) > [data-label='Patientennummer'] .is-no-longer-eligible").should("exist");
    });

    it("can update and save recruitment status", () => {
      cy.get(":nth-child(1) > [data-label='Status'] > .dropdown > .dropdown-trigger > .button")
        .click()
        .get(":nth-child(1) > [data-label='Status'] > .dropdown > .dropdown-menu > .dropdown-content > :nth-child(4)")
        .click()
        .get(":nth-child(1) > [data-label='Aktionen'] .save-status")
        .click();

      cy.wait("@patchSubject", { timeout: 30000 });
      // hmm, this is not a great workaround, but it seems it takes
      // some time for the FHIR server to respond with the updated resource
      // maybe some sort of caching issue.
      cy.wait(5000);
      cy.reload();

      cy.get(":nth-child(1) > [data-label='Status'] > .dropdown > .dropdown-trigger > .button").contains(
        "Wurde eingeschlossen"
      );
    });
  });
});
