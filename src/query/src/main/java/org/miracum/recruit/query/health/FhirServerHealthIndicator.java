package org.miracum.recruit.query.health;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import org.hl7.fhir.r4.model.CapabilityStatement;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.stereotype.Component;

@Component
public class FhirServerHealthIndicator implements HealthIndicator {

  private final String fhirServerBase;
  private final IGenericClient fhirClient;

  public FhirServerHealthIndicator(IGenericClient fhirClient) {
    fhirServerBase = fhirClient.getServerBase();
    this.fhirClient = fhirClient;
  }

  @Override
  public Health health() {
    try {
      fhirClient.capabilities().ofType(CapabilityStatement.class).execute();
    } catch (Exception exc) {
      return Health.down()
          .withDetail("baseUrl", fhirServerBase)
          .withDetail("error", exc.getMessage())
          .build();
    }

    return Health.up().withDetail("baseUrl", fhirServerBase).build();
  }
}
