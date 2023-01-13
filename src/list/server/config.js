exports.config = {
  pseudonymization: {
    enabled: process.env.DE_PSEUDONYMIZATION_ENABLED === "true" || process.env.DE_PSEUDONYMIZATION_ENABLED === "1",
    url: process.env.DE_PSEUDONYMIZATION_SERVICE_URL || "http://localhost:5000/fhir",
    apiKey: process.env.DE_PSEUDONYMIZATION_API_KEY || "fhir-pseudonymizer-api-key",
    timeoutMs: process.env.DE_PSEUDONYMIZATION_REQUEST_TIMEOUT_MS || 30_000,
  },
  tracing: {
    enabled: process.env.TRACING_ENABLED === "true" || process.env.TRACING_ENABLED === "1",
    serviceName: process.env.JAEGER_SERVICE_NAME || process.env.OTEL_SERVICE_NAME || "screeninglist",
  },
  shouldLogRequests: process.env.LOG_REQUESTS === "true" || process.env.LOG_REQUESTS === "1",
  metrics: {
    bearerToken: process.env.METRICS_BEARER_TOKEN,
  },
  fhirUrl: process.env.FHIR_URL || "http://localhost:8082/fhir",
  auth: {
    url: process.env.KEYCLOAK_AUTH_URL || "http://localhost:8083/auth",
    clientId: process.env.KEYCLOAK_CLIENT_ID || "uc1-screeninglist",
    realm: process.env.KEYCLOAK_REALM || "MIRACUM",
    disabled: process.env.KEYCLOAK_DISABLED === "true" || process.env.KEYCLOAK_DISABLED === "1",
    checkLoginIframeDisabled:
      process.env.KEYCLOAK_CHECK_LOGIN_IFRAME_DISABLED === "true" || process.env.KEYCLOAK_CHECK_LOGIN_IFRAME_DISABLED === "1",
  },
  ui: {
    hideDemographics: process.env.HIDE_DEMOGRAPHICS === "true" || process.env.HIDE_DEMOGRAPHICS === "1",
    hideLastVisit: process.env.HIDE_LAST_VISIT === "true" || process.env.HIDE_LAST_VISIT === "1",
    hideEhrButton: process.env.HIDE_EHR_BUTTON === "true" || process.env.HIDE_EHR_BUTTON === "1",
    hideRecommendationDate: process.env.HIDE_RECOMMENDATION_DATE === "true" || process.env.HIDE_RECOMMENDATION_DATE === "1",
  },
  proxy: {
    isSecureBackend: process.env.PROXY_IS_SECURE_BACKEND === "true" || process.env.PROXY_IS_SECURE_BACKEND === "1",
  },
  rulesFilePath: process.env.RULES_FILE_PATH || "./notify-rules.dev.yaml",
};
