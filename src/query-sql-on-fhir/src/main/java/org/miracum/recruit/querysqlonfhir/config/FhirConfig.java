package org.miracum.recruit.querysqlonfhir.config;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.okhttp.client.OkHttpRestfulClientFactory;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.binder.okhttp3.OkHttpMetricsEventListener;
import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.instrumentation.okhttp.v3_0.OkHttpTelemetry;
import java.time.Duration;
import okhttp3.OkHttpClient;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Primary;

@Configuration
public class FhirConfig {

  @Bean
  public FhirContext fhirContext(
      @Value("${fhir.server.timeout-in-seconds}") long fhirServerTimeoutSeconds,
      OpenTelemetry openTelemetry) {
    var fhirContext = FhirContext.forR4();

    var okclient =
        new OkHttpClient.Builder()
            .callTimeout(Duration.ofSeconds(fhirServerTimeoutSeconds))
            .connectTimeout(Duration.ofSeconds(fhirServerTimeoutSeconds))
            .readTimeout(Duration.ofSeconds(fhirServerTimeoutSeconds))
            .writeTimeout(Duration.ofSeconds(fhirServerTimeoutSeconds))
            .eventListener(
                OkHttpMetricsEventListener.builder(Metrics.globalRegistry, "fhir.client").build())
            .build();
    var okHttpFactory = new OkHttpRestfulClientFactory(fhirContext);
    // This OkHttpClient is built by hand rather than as a Spring bean, so the OpenTelemetry
    // Spring Boot starter's classpath-based auto-instrumentation (JDBC, @Scheduled, ...) can't
    // reach it - wrap its Call.Factory explicitly instead. setHttpClient(Object) casts to
    // okhttp3.Call.Factory internally, which this wrapper also implements, so it's a drop-in
    // replacement for the plain okclient used before.
    okHttpFactory.setHttpClient(
        OkHttpTelemetry.builder(openTelemetry).build().createCallFactory(okclient));

    fhirContext.setRestfulClientFactory(okHttpFactory);
    return fhirContext;
  }

  @Bean
  @Primary
  public IGenericClient fhirClient(
      FhirContext fhirContext, @Value("${fhir.url}") String fhirServerUrl) {
    return fhirContext.newRestfulGenericClient(fhirServerUrl);
  }

  @Bean
  public IGenericClient sqlOnFhirClient(
      FhirContext fhirContext, @Value("${sql-on-fhir.url}") String sqlOnFhirServerUrl) {
    return fhirContext.newRestfulGenericClient(sqlOnFhirServerUrl);
  }
}
