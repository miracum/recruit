// https://docs.cypress.io/api/introduction/api.html
import { oidcConfig } from "../fixtures/keycloak";

const patientRequestUrl = "**/Patient/**";

describe("PatientRecord", () => {
  beforeEach(() => {
    cy.login(oidcConfig);
  });
  context("after loading lists from server", () => {
    beforeEach(() => {
      cy.intercept({
        method: "GET",
        path: patientRequestUrl,
      }).as("getRecord");

      cy.visit("/patients/1/record", {
        onBeforeLoad: (win) => {
          // eslint-disable-next-line no-param-reassign
          win.fetch = null;
        },
      });
      cy.wait("@getRecord");
    });

    it("should display the patient's medical record number", () => {
      cy.get("h1", { timeout: 30000 }).should(
        "contain.text",
        "Patient 3354978E05D57044AA924A221928938F9C97179FD6D9E6E" +
          "70CD199D4A31D9B21EFB5618A653B04DAA1E38D4FCE73BAD95DD26099C6C8F987F6332D8E21F6E48F"
      );
    });
  });
});
