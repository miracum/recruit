package org.miracum.recruit.notify.scheduler;

import org.miracum.recruit.notify.message.MessageDistributor;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;

/** Service that calls message distribution if timer event is fired. */
@Service
public class NotifyMessageSchedulerService {
  private final MessageDistributor messageDistributor;

  @Autowired
  public NotifyMessageSchedulerService(MessageDistributor messageDistributor) {
    this.messageDistributor = messageDistributor;
  }

  /** Job to distribute messages based on jobKey (trigger name in config) will be executed. */
  public void executeMessageDistributionJob(String jobKey) {
    messageDistributor.distribute(jobKey);
  }
}
