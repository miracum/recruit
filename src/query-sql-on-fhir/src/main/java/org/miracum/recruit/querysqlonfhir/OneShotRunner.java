package org.miracum.recruit.querysqlonfhir;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.stereotype.Component;

/**
 * Alternative run mode for driving this module as a single Job/Workflow step (e.g. an Argo Workflow
 * template) instead of a long-running scheduled service: runs exactly one poll cycle, then exits -
 * with a non-zero status if anything failed, so the orchestrator can detect and react to (e.g.
 * retry, alert on) a failed run. Enabled via {@code query-sql-on-fhir.run-mode=one-shot}; mutually
 * exclusive with {@link ScheduledPolling}.
 */
@Component
@ConditionalOnProperty(prefix = "query-sql-on-fhir", name = "run-mode", havingValue = "one-shot")
public class OneShotRunner implements ApplicationRunner {

  private static final Logger log = LoggerFactory.getLogger(OneShotRunner.class);

  private final PollForStudies pollForStudies;
  private final ConfigurableApplicationContext context;

  public OneShotRunner(PollForStudies pollForStudies, ConfigurableApplicationContext context) {
    this.pollForStudies = pollForStudies;
    this.context = context;
  }

  @Override
  public void run(ApplicationArguments args) {
    var exitCode = runPollCycle();

    // SpringApplication.exit closes the context (releasing the FHIR/Trino clients) and resolves
    // the exit code; System.exit forces the JVM to actually stop, regardless of any non-daemon
    // threads (e.g. the embedded web server) that would otherwise keep it alive.
    System.exit(SpringApplication.exit(context, () -> exitCode));
  }

  private int runPollCycle() {
    try {
      var result = pollForStudies.pollForStudies();
      if (result.hasFailures()) {
        log.error(
            "One-shot run finished with {} of {} studies failing to process; exiting with a"
                + " non-zero status.",
            result.studiesFailed(),
            result.studiesFound());
        return 1;
      }

      log.info("One-shot run finished successfully: {} studies processed.", result.studiesFound());
      return 0;
    } catch (Exception ex) {
      // e.g. the initial ResearchStudy search itself failed, before any per-study processing
      // (and thus before PollForStudies' own per-study error isolation) could even begin.
      log.error("One-shot run failed", ex);
      return 1;
    }
  }
}
