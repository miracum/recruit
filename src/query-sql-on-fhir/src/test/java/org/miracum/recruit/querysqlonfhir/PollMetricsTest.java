package org.miracum.recruit.querysqlonfhir;

import static org.assertj.core.api.Assertions.assertThat;

import io.micrometer.core.instrument.simple.SimpleMeterRegistry;
import org.junit.jupiter.api.Test;

class PollMetricsTest {

  @Test
  void recordStudy_tagsTheTimerWithStudyAndOutcome() {
    var registry = new SimpleMeterRegistry();
    var metrics = new PollMetrics(registry);

    metrics.recordStudy(metrics.startTimer(), "DEMO", true);
    metrics.recordStudy(metrics.startTimer(), "DEMO", false);
    metrics.recordStudy(metrics.startTimer(), "DEMO", true);

    assertThat(
            registry
                .get("recruit.query.study")
                .tag("study", "DEMO")
                .tag("outcome", "success")
                .timer()
                .count())
        .isEqualTo(2);
    assertThat(registry.get("recruit.query.study").tag("outcome", "failure").timer().count())
        .isEqualTo(1);
  }

  @Test
  void recordPoll_tagsTheTimerWithOutcome() {
    var registry = new SimpleMeterRegistry();
    var metrics = new PollMetrics(registry);

    metrics.recordPoll(metrics.startTimer(), false);

    assertThat(registry.get("recruit.query.poll").tag("outcome", "failure").timer().count())
        .isEqualTo(1);
  }

  @Test
  void setCandidates_keepsOneGaugePerStudyAtItsLatestValue() {
    var registry = new SimpleMeterRegistry();
    var metrics = new PollMetrics(registry);

    metrics.setCandidates("A", 10);
    metrics.setCandidates("B", 3);
    metrics.setCandidates("A", 7);

    assertThat(registry.get("recruit.query.study.candidates").tag("study", "A").gauge().value())
        .isEqualTo(7);
    assertThat(registry.get("recruit.query.study.candidates").tag("study", "B").gauge().value())
        .isEqualTo(3);
    assertThat(registry.get("recruit.query.study.candidates").gauges()).hasSize(2);
  }
}
