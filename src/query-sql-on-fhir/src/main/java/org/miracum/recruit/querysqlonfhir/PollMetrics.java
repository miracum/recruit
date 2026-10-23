package org.miracum.recruit.querysqlonfhir;

import io.micrometer.core.instrument.Gauge;
import io.micrometer.core.instrument.MeterRegistry;
import io.micrometer.core.instrument.Timer;
import java.time.Duration;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicLong;
import org.springframework.stereotype.Component;

/**
 * The query module's own metrics, next to Spring Boot's (HTTP server, JVM, {@code @Scheduled}
 * tasks) and the FHIR client's ({@code fhir.client}, see {@code FhirConfig}). Exported at {@code
 * /actuator/prometheus} as {@code recruit_query_*} and charted by the chart's Grafana dashboard
 * (charts/recruit/files/dashboards). Studies are tagged by acronym - there are only ever a handful.
 */
@Component
public class PollMetrics {

  private static final String OUTCOME_TAG = "outcome";
  private static final String STUDY_TAG = "study";

  private final MeterRegistry registry;
  private final Map<String, AtomicLong> candidatesByStudy = new ConcurrentHashMap<>();

  public PollMetrics(MeterRegistry registry) {
    this.registry = registry;
  }

  public Timer.Sample startTimer() {
    return Timer.start(registry);
  }

  /** One whole poll cycle; a failure is a cycle where at least one study failed, or the search. */
  public void recordPoll(Timer.Sample sample, boolean succeeded) {
    sample.stop(
        timer("recruit.query.poll", "How long one poll cycle took, across every study.")
            .tag(OUTCOME_TAG, outcome(succeeded))
            .register(registry));
  }

  /** One study's eligibility evaluation, including writing its results to the FHIR server. */
  public void recordStudy(Timer.Sample sample, String study, boolean succeeded) {
    sample.stop(
        timer(
                "recruit.query.study",
                "How long evaluating one study's criteria and writing the results took.")
            .tag(STUDY_TAG, study)
            .tag(OUTCOME_TAG, outcome(succeeded))
            .register(registry));
  }

  /** The candidate count on the study's screening list as of its last successful evaluation. */
  public void setCandidates(String study, int count) {
    candidatesByStudy
        .computeIfAbsent(
            study,
            key -> {
              var holder = new AtomicLong();
              Gauge.builder("recruit.query.study.candidates", holder, AtomicLong::get)
                  .description("Patients on the study's screening list after its last evaluation.")
                  .tag(STUDY_TAG, key)
                  .register(registry);
              return holder;
            })
        .set(count);
  }

  // Polls take anywhere from well under a second (DuckDB on a handful of patients) to many
  // minutes (Trino on a full data platform) - the default histogram tops out at 30s.
  private static Timer.Builder timer(String name, String description) {
    return Timer.builder(name)
        .description(description)
        .publishPercentileHistogram()
        .minimumExpectedValue(Duration.ofMillis(50))
        .maximumExpectedValue(Duration.ofHours(2));
  }

  private static String outcome(boolean succeeded) {
    return succeeded ? "success" : "failure";
  }
}
