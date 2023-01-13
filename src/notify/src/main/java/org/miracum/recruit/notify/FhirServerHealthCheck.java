package org.miracum.recruit.notify;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import org.hl7.fhir.r4.model.CapabilityStatement;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.stereotype.Component;

/** Fhir Server Health Check Indicator. */
@Component
public final class FhirServerHealthCheck implements HealthIndicator {

  private final IGenericClient fhirClient;

  @Autowired
  FhirServerHealthCheck(final IGenericClient fhirClient) {
    this.fhirClient = fhirClient;
  }

  @Override
  public Health health() {
    try {
      fhirClient.capabilities().ofType(CapabilityStatement.class).execute();
    } catch (Exception exc) {
      return Health.down().withDetail("Error Message", exc.getMessage()).build();
    }

    return Health.up().build();
  }
}
