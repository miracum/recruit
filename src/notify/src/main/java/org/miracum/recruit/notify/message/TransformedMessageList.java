package org.miracum.recruit.notify.message;

import java.util.List;

public class TransformedMessageList {

  List<TransformedMessages> transformedMessages;

  public TransformedMessageList(List<TransformedMessages> transformedMessages) {
    this.transformedMessages = transformedMessages;
  }

  public List<TransformedMessages> getTransformedMessages() {
    return transformedMessages;
  }
}
