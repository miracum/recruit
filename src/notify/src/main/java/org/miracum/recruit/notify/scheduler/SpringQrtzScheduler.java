package org.miracum.recruit.notify.scheduler;

import static net.logstash.logback.argument.StructuredArguments.kv;

import java.util.HashSet;
import org.miracum.recruit.notify.mailconfig.UserConfig;
import org.quartz.CronExpression;
import org.quartz.CronScheduleBuilder;
import org.quartz.JobBuilder;
import org.quartz.JobDetail;
import org.quartz.Scheduler;
import org.quartz.SchedulerException;
import org.quartz.Trigger;
import org.quartz.TriggerBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.quartz.SchedulerFactoryBean;

/** Quartz Scheduler initializing scheduled job for each defined timer in app config. */
@Configuration
public class SpringQrtzScheduler {
  private static final Logger LOG = LoggerFactory.getLogger(SpringQrtzScheduler.class);

  private final UserConfig config;

  @Autowired
  SpringQrtzScheduler(UserConfig config) {
    this.config = config;
  }

  @Bean
  public Scheduler scheduler(SchedulerFactoryBean factory) throws SchedulerException {
    var scheduler = factory.getScheduler();

    var job = createJobDetail("notifySubscribers", "notify");
    var triggers = new HashSet<Trigger>();
    for (var schedule : config.getSchedules().entrySet()) {
      var trigger = createTrigger(schedule.getKey(), schedule.getValue());

      LOG.debug(
          "adding {} at {} for job {}",
          kv("trigger", schedule.getKey()),
          kv("cron", schedule.getValue(), "{0}=\"{1}\""),
          kv("job", job.getKey()));

      triggers.add(trigger);
    }

    scheduler.scheduleJob(job, triggers, true);

    LOG.debug("starting scheduler instance");
    scheduler.start();

    return scheduler;
  }

  private JobDetail createJobDetail(String jobName, String groupName) {
    return JobBuilder.newJob(NotifySubscribersJob.class)
        .withIdentity(jobName, groupName)
        .storeDurably(true)
        .build();
  }

  private Trigger createTrigger(String triggerName, CronExpression cronExpression) {
    return TriggerBuilder.newTrigger()
        .withIdentity(triggerName)
        .withSchedule(CronScheduleBuilder.cronSchedule(cronExpression))
        .build();
  }
}
