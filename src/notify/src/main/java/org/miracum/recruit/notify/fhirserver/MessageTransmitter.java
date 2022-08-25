package org.miracum.recruit.notify.fhirserver;

import static net.logstash.logback.argument.StructuredArguments.kv;

import java.util.List;
import java.util.UUID;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.CommunicationRequest;
import org.hl7.fhir.r4.model.ResourceType;
import org.miracum.recruit.notify.FhirServerProvider;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;

/** Create list of messages in FHIR server to store them temporary. */
@Service
public class MessageTransmitter {

  private static final Logger LOG = LoggerFactory.getLogger(MessageTransmitter.class);

  final FhirServerProvider fhirClient;

  @Autowired
  public MessageTransmitter(FhirServerProvider fhirClient) {
    this.fhirClient = fhirClient;
  }

  /** Save message list to target FHIR server. */
  public void transmit(List<CommunicationRequest> messages) {
    LOG.info("transmit message list to fhir server.");

    if (messages.isEmpty()) {
      LOG.warn("no messages specified to send");
      return;
    }

    var bundle = new Bundle();
    bundle.setType(Bundle.BundleType.TRANSACTION);

    for (var message : messages) {
      var topic = message.getReasonCodeFirstRep().getText();
      var messageUuid = UUID.randomUUID();
      LOG.debug(
          "adding CommunicationRequest for {} as {} to transaction",
          kv("topic", topic),
          kv("messageIdentifier", message.getIdentifierFirstRep().getValue()));

      bundle
          .addEntry()
          .setResource(message)
          .setFullUrl("urn:uuid:" + messageUuid)
          .getRequest()
          .setUrl(ResourceType.CommunicationRequest.name())
          .setMethod(Bundle.HTTPVerb.POST);
    }

    try {
      fhirClient.executeTransaction(bundle);
    } catch (Exception exc) {
      LOG.error("failed to create the CommunicationRequest resources", exc);
    }
  }
}
