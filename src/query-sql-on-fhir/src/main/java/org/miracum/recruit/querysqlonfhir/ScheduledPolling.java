package org.miracum.recruit.querysqlonfhir;

import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

/**
 * Default run mode: triggers {@link PollForStudies} on the configured cron schedule and keeps
 * running until killed. Disabled when {@code query-sql-on-fhir.run-mode} is set to {@code one-shot}
 * (see {@link OneShotRunner}), so the two modes never both trigger a poll cycle.
 */
@Component
@ConditionalOnProperty(
    prefix = "query-sql-on-fhir",
    name = "run-mode",
    havingValue = "scheduled",
    matchIfMissing = true)
public class ScheduledPolling {

  private final PollForStudies pollForStudies;

  public ScheduledPolling(PollForStudies pollForStudies) {
    this.pollForStudies = pollForStudies;
  }

  @Scheduled(cron = "${query-sql-on-fhir.schedule.cron}")
  public void poll() {
    pollForStudies.pollForStudies();
  }
}
