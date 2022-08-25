package org.miracum.recruit.notify.mailsender;

import lombok.Data;

/** Data structure to store study acronym and referring screening list link. */
@Data
public class NotifyInfo {
  String studyAcronym;
  String screeningListLink;
}
