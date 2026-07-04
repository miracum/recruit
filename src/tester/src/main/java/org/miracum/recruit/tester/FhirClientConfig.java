package org.miracum.recruit.tester;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class FhirClientConfig {

  @Bean
  public FhirContext fhirContext() {
    return FhirContext.forR4();
  }

  @Bean
  public IGenericClient fhirClient(FhirContext fhirContext, TesterProperties properties) {
    return fhirContext.newRestfulGenericClient(properties.fhirServerBaseUrl().toString());
  }
}
