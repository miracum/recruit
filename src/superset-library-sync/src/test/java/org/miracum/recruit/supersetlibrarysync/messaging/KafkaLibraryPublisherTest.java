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
import org.springframework.kafka.support.KafkaHeaders;
import org.springframework.messaging.Message;

class KafkaLibraryPublisherTest {

  @Test
  void publish_sendsTheBundleItselfToTheConfiguredTopic() {
    var streamBridge = mock(StreamBridge.class);
    when(streamBridge.send(eq("library-updates"), any(Message.class))).thenReturn(true);
    var sut =
        new KafkaLibraryPublisher(
            streamBridge, new KafkaPublishProperties(true, "library-updates"));
    var bundle = new Bundle();

    var result = sut.publish(bundle);

    assertThat(result).isTrue();
    var message = org.mockito.ArgumentCaptor.forClass(Message.class);
    verify(streamBridge).send(eq("library-updates"), message.capture());
    assertThat(message.getValue().getPayload()).isSameAs(bundle);
    assertThat(message.getValue().getHeaders().get(KafkaHeaders.MESSAGE_KEY))
        .isEqualTo(bundle.getId());
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
