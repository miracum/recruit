package org.miracum.recruit.supersetlibrarysync.messaging;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;

/**
 * Controls the optional Kafka publishing feature (see {@link KafkaLibraryPublisher}): when {@code
 * enabled}, every synced Library's PUT bundle is sent, as FHIR+JSON, to {@code topic} instead of
 * being submitted to the FHIR server. Disabled by default, since most deployments only need the
 * FHIR server. Kafka connectivity itself (brokers, security, etc.) is configured separately via
 * Spring Boot's own {@code spring.kafka.*} properties.
 */
@ConfigurationProperties(prefix = "sync.kafka")
public record KafkaPublishProperties(@DefaultValue("false") boolean enabled, String topic) {

  public KafkaPublishProperties {
    if (enabled && (topic == null || topic.isBlank())) {
      throw new IllegalArgumentException(
          "sync.kafka.topic must be set when sync.kafka.enabled is true");
    }
  }
}
