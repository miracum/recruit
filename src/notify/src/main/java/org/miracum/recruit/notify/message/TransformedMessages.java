package org.miracum.recruit.notify.message;

import java.util.ArrayList;
import java.util.List;

public class TransformedMessages {

  String emailAddress;
  String studyName;
  List<Message> messages;

  public TransformedMessages(String emailAddress, String studyName) {
    this.emailAddress = emailAddress;
    this.studyName = studyName;
    this.messages = new ArrayList<Message>();
  }

  public List<Message> getMessages() {
    return messages;
  }

  public void setMessages(List<Message> messages) {
    this.messages = messages;
  }

  public String getEmailAddress() {
    return emailAddress;
  }

  public String getStudyName() {
    return studyName;
  }
}
