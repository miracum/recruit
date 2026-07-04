package org.miracum.recruit.tester;

import java.net.URI;
import java.time.Duration;
import java.util.Map;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;

@ConfigurationProperties
public record TesterProperties(
    @DefaultValue("http://localhost:8025/api/") URI mailhogApiBaseUrl,
    @DefaultValue("5") int expectedNumberOfMessages,
    @DefaultValue("10") int retries,
    String fhirResourceBundle,
    @DefaultValue("http://localhost:8082/fhir") URI fhirServerBaseUrl,
    @DefaultValue("30s") Duration totalDuration,
    @DefaultValue("5") int sendCount,
    Map<String, Integer> expectedResourceCounts) {}
