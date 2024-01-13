import axios from "axios";
import * as promClient from "prom-client";
import * as rax from "retry-axios";

const dePseudonymizationDurationHistogram = new promClient.Histogram({
  name: "list_de_pseudonymization_duration_seconds",
  help: "Histogram for the FHIR resource de-pseudonymization duration",
});

rax.attach();

export async function dePseudonymize(config, resource) {
  const end = dePseudonymizationDurationHistogram.startTimer();
  const response = await axios.post(`${config.url}/$de-pseudonymize`, resource, {
    headers: { "x-api-key": config.apiKey, "Content-Type": "application/fhir+json" },
    timeout: config.timeoutMs,
  });
  end();
  return response.data;
}
