package org.miracum.recruit.supersetlibrarysync.messaging;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import org.hl7.fhir.r4.model.Bundle;
import org.junit.jupiter.api.Test;
import org.springframework.cloud.stream.function.StreamBridge;

class KafkaLibraryPublisherTest {

  @Test
  void publish_sendsTheBundleItselfToTheConfiguredTopic() {
    var streamBridge = mock(StreamBridge.class);
    when(streamBridge.send(eq("library-updates"), any(Bundle.class))).thenReturn(true);
    var sut =
        new KafkaLibraryPublisher(
            streamBridge, new KafkaPublishProperties(true, "library-updates"));
    var bundle = new Bundle();

    var result = sut.publish(bundle);

    assertThat(result).isTrue();
    // sent natively as the Bundle object itself, not a pre-encoded JSON string - the actual
    // FHIR+JSON encoding is left to the configured Kafka value-serializer.
    verify(streamBridge).send("library-updates", bundle);
  }

  @Test
  void publish_returnsFalseWhenTheBinderRejectsTheSend() {
    var streamBridge = mock(StreamBridge.class);
    when(streamBridge.send(any(), any())).thenReturn(false);
    var sut =
        new KafkaLibraryPublisher(
            streamBridge, new KafkaPublishProperties(true, "library-updates"));

    var result = sut.publish(new Bundle());

    assertThat(result).isFalse();
  }
}
