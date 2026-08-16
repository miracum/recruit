package org.miracum.recruit.supersetlibrarysync.config;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.okhttp.client.OkHttpRestfulClientFactory;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.time.Duration;
import okhttp3.OkHttpClient;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class FhirConfig {

  @Bean
  public FhirContext fhirContext(
      @Value("${fhir.server.timeout-in-seconds}") long fhirServerTimeoutSeconds) {
    var fhirContext = FhirContext.forR4();

    var okHttpClient =
        new OkHttpClient.Builder()
            .callTimeout(Duration.ofSeconds(fhirServerTimeoutSeconds))
            .connectTimeout(Duration.ofSeconds(fhirServerTimeoutSeconds))
            .readTimeout(Duration.ofSeconds(fhirServerTimeoutSeconds))
            .writeTimeout(Duration.ofSeconds(fhirServerTimeoutSeconds))
            .build();
    var okHttpFactory = new OkHttpRestfulClientFactory(fhirContext);
    okHttpFactory.setHttpClient(okHttpClient);

    fhirContext.setRestfulClientFactory(okHttpFactory);
    return fhirContext;
  }

  @Bean
  public IGenericClient fhirClient(
      FhirContext fhirContext, @Value("${fhir.url}") String fhirServerUrl) {
    return fhirContext.newRestfulGenericClient(fhirServerUrl);
  }
}
