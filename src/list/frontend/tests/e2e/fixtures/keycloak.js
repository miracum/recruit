const oidcConfig = {
  root: Cypress.env("KEYCLOAK_URL") || "http://localhost:8083",
  realm: "MIRACUM",
  username: "user1",
  password: "user1",
  client_id: "uc1-screeninglist",
  redirect_uri: Cypress.env("REDIRECT_URL") || "http://localhost:8080/",
};

// using the object spread operator causes the following error:
// objectSpread.js:1
// import defineProperty from "./defineProperty.js";
// ^
// ParseError: 'import' and 'export' may appear only with 'sourceType: module'

// eslint-disable-next-line prefer-object-spread
const adminOidcConfig = Object.assign({}, oidcConfig);
adminOidcConfig.username = "uc1-admin";
adminOidcConfig.password = "admin";

export { oidcConfig, adminOidcConfig };
