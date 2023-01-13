package org.miracum.recruit.notify.mailsender;

import lombok.Data;

@Data
public class MailInfo {
  String from;
  String to;
  String subject;
}
