const axios = require("axios");
const promClient = require("prom-client");
const rax = require("retry-axios");

const dePseudonymizationDurationHistogram = new promClient.Histogram({
  name: "list_de_pseudonymization_duration_seconds",
  help: "Histogram for the FHIR resource de-pseudonymization duration",
});

rax.attach();

const dePseudonymize = async (config, resource) => {
  const end = dePseudonymizationDurationHistogram.startTimer();
  const response = await axios.post(`${config.url}/$de-pseudonymize`, resource, {
    headers: { "x-api-key": config.apiKey, "Content-Type": "application/fhir+json" },
    timeout: config.timeoutMs,
  });
  end();
  return response.data;
};

exports.dePseudonymize = dePseudonymize;
