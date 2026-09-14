package org.miracum.recruit.supersetlibrarysync;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;

/**
 * Runs exactly one sync cycle and exits - a batch job, not a service, following the same one-shot
 * shape as {@code tester}'s {@code TesterApplication} in this repo. Unlike {@code
 * query-sql-on-fhir} (which supports both a scheduled and a one-shot run mode), this module has
 * only one: periodic execution, if wanted, is left to an external scheduler (e.g. a Kubernetes
 * CronJob), not a {@code @Scheduled} method here.
 */
@SpringBootApplication
@ConfigurationPropertiesScan
public class SupersetLibrarySyncApplication implements ApplicationRunner {

  private static final Logger log = LoggerFactory.getLogger(SupersetLibrarySyncApplication.class);

  private final SyncService syncService;

  public SupersetLibrarySyncApplication(SyncService syncService) {
    this.syncService = syncService;
  }

  public static void main(String[] args) {
    new SpringApplicationBuilder(SupersetLibrarySyncApplication.class)
        .web(WebApplicationType.NONE)
        .run(args);
  }

  @Override
  public void run(ApplicationArguments args) throws Exception {
    var result = syncService.sync();

    log.info(
        "Sync finished: {} saved quer{} considered, {} synced, {} failed.",
        result.savedQueriesConsidered(),
        result.savedQueriesConsidered() == 1 ? "y" : "ies",
        result.synced(),
        result.failed());

    if (result.hasFailures()) {
      throw new IllegalStateException(
          "%d of %d saved quer%s failed to sync."
              .formatted(
                  result.failed(),
                  result.savedQueriesConsidered(),
                  result.savedQueriesConsidered() == 1 ? "y" : "ies"));
    }
  }
}
