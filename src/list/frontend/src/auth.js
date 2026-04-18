let keycloakInstance = null;

export function setKeycloak(keycloak) {
  keycloakInstance = keycloak;
}

export function getToken() {
  return keycloakInstance?.token;
}
