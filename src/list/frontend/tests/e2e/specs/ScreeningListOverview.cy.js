// https://docs.cypress.io/api/introduction/api.html
import { oidcConfig, adminOidcConfig } from "../fixtures/keycloak";

const listRequestUrl = "**/List/**";

describe("ScreeningListOverview as a regular user", () => {
  beforeEach(() => {
    cy.login(oidcConfig);
  });
  afterEach(() => {
    cy.logout(oidcConfig);
  });
  context("after loading lists from server", () => {
    beforeEach(() => {
      cy.intercept({
        method: "GET",
        path: listRequestUrl,
      }).as("getLists");

      cy.visit("/", {
        onBeforeLoad: (win) => {
          // eslint-disable-next-line no-param-reassign
          win.fetch = null;
        },
      });
      cy.wait("@getLists");
    });

    it("three cards for the sample studies exist", () => {
      cy.get(".card", { timeout: 30000 }).should("have.length", 3);
    });

    it("displays the PROSa, AMICA, and SECRET studies", () => {
      cy.contains("PROSa");
      cy.contains("AMICA");
      cy.contains("SECRET");
    });

    it("doesn't show inactive lists", () => {
      cy.contains("RETIRED").should("not.exist");
    });

    it("doesn't show toggles to change the list status", () => {
      cy.get("label.switch").should("not.exist");
    });

    // we can't easily test this since it requires the server components to be running and
    // filtering out the resource before display
    // it("does not display the inaccessible SECRET study", () => {
    //   cy.not.contains("SECRET");
    // });
  });
});

describe("ScreeningListOverview as an admin user", () => {
  beforeEach(() => {
    cy.login(adminOidcConfig);
  });
  afterEach(() => {
    cy.logout(adminOidcConfig);
  });
  context("after loading lists from server", () => {
    beforeEach(() => {
      cy.intercept({
        method: "GET",
        path: listRequestUrl,
      }).as("getLists");

      cy.visit("/", {
        onBeforeLoad: (win) => {
          // eslint-disable-next-line no-param-reassign
          win.fetch = null;
        },
      });
      cy.wait("@getLists");
    });

    it("does show inactive lists", () => {
      cy.contains("RETIRED").should("not.exist");
    });

    it("does show toggles to change the list status", () => {
      cy.get("label.switch").should("exist");
    });

    it("moves inactive study to list of active ones when clicking the toggle button", () => {
      cy.get(".inactive-screening-lists > .screening-list-card > .card-content > .media > .media-right > .field").click();

      cy.get(".active-screening-lists > .card", { timeout: 30000 }).should("have.length", 4);
      cy.get(".inactive-screening-lists > .card", { timeout: 30000 }).should("have.length", 0);
    });

    it("moves active study back to list of inactive ones when clicking the toggle button", () => {
      cy.get(".active-screening-lists > :nth-child(5) > .card-content > .media > .media-right > .field > .switch").click();

      cy.get(".active-screening-lists > .card", { timeout: 30000 }).should("have.length", 3);
      cy.get(".inactive-screening-lists > .card", { timeout: 30000 }).should("have.length", 1);
    });
  });
});
