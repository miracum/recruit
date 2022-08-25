package org.miracum.recruit.notify.scheduler;

import org.quartz.Job;
import org.quartz.JobExecutionContext;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.stereotype.Component;

@Component
public class NotifySubscribersJob implements Job {
  private static final Logger LOG = LoggerFactory.getLogger(NotifySubscribersJob.class);

  private NotifyMessageSchedulerService jobService;

  public NotifySubscribersJob(NotifyMessageSchedulerService jobService) {
    this.jobService = jobService;
  }

  @Override
  public void execute(JobExecutionContext context) {
    MDC.put("job", context.getJobDetail().getKey().getName());
    MDC.put("trigger", context.getTrigger().getKey().getName());
    LOG.debug("scheduled execution time reached");

    var triggerName = context.getTrigger().getKey().getName();
    jobService.executeMessageDistributionJob(triggerName);
  }
}
