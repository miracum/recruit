package org.miracum.recruit.notify.message;

import java.util.Date;
import org.hl7.fhir.r4.model.CommunicationRequest;

public class Message {

  Date authoredTimestamp;
  String fhirMessageId;
  CommunicationRequest originalMessage;

  public Message(
      Date authoredTimestamp, String fhirMessageId, CommunicationRequest originalMessage) {
    super();
    this.authoredTimestamp = authoredTimestamp;
    this.fhirMessageId = fhirMessageId;
    this.originalMessage = originalMessage;
  }

  public Date getAuthoredTimestamp() {
    return authoredTimestamp;
  }

  public String getFhirMessageId() {
    return fhirMessageId;
  }

  public CommunicationRequest getOriginalMessage() {
    return originalMessage;
  }
}
