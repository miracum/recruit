package org.miracum.recruit.supersetlibrarysync.messaging;

import org.hl7.fhir.r4.model.Bundle;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.cloud.stream.function.StreamBridge;
import org.springframework.kafka.support.KafkaHeaders;
import org.springframework.messaging.support.MessageBuilder;
import org.springframework.stereotype.Component;

/**
 * Publishes a Library's single-entry PUT {@link Bundle} (see {@code SqlLibraryBuilder#build}) to
 * the configured Kafka topic, <em>instead of</em> submitting it to the FHIR server.
 *
 * <p>Only registered when {@code sync.kafka.enabled} is {@code true} (see {@link
 * KafkaPublishProperties}); {@code SyncService} takes it as an {@code Optional} collaborator and,
 * when present, uses it in place of - not alongside - the usual FHIR server submission, so a sync
 * cycle behaves exactly as before when this feature is off, which is the default.
 *
 * <p>Sends the {@code Bundle} object itself (not a pre-encoded string) as the message payload, with
 * the Bundle id as the Kafka message key. The configured topic name is used as the destination
 * directly rather than a statically declared {@code spring.cloud.stream.bindings.*} output binding,
 * since the topic is only known once {@link KafkaPublishProperties} is bound. The actual FHIR+JSON
 * encoding is left to the Kafka producer's {@code value-serializer} (see {@code application.yml}'s
 * {@code spring.kafka.producer.value-serializer}) - {@code
 * org.miracum.kafka.serializers.KafkaFhirSerializer}, the same one {@code
 * miracum/kafka-fhir-to-server} and {@code miracum/fhir-gateway} use, with {@code
 * spring.cloud.stream.default.producer.use-native-encoding} telling the binder to hand off to it
 * instead of doing its own message conversion.
 */
@Component
@ConditionalOnProperty(prefix = "sync.kafka", name = "enabled", havingValue = "true")
public class KafkaLibraryPublisher {

  private static final Logger log = LoggerFactory.getLogger(KafkaLibraryPublisher.class);

  private final StreamBridge streamBridge;
  private final String topic;

  public KafkaLibraryPublisher(StreamBridge streamBridge, KafkaPublishProperties properties) {
    this.streamBridge = streamBridge;
    this.topic = properties.topic();
    log.info("Kafka publishing enabled: Library bundles will be sent to topic '{}'.", topic);
  }

  /** Returns whether the send was accepted by the binder, per {@link StreamBridge#send}. */
  public boolean publish(Bundle libraryPutBundle) {
    var message =
        MessageBuilder.withPayload(libraryPutBundle)
            .setHeader(KafkaHeaders.MESSAGE_KEY, libraryPutBundle.getId())
            .build();
    var sent = streamBridge.send(topic, message);
    if (!sent) {
      log.error(
          "Failed to publish a Library bundle to Kafka topic '{}': {}",
          topic,
          libraryPutBundle.getEntryFirstRep().getRequest().getUrl());
    }
    return sent;
  }
}
