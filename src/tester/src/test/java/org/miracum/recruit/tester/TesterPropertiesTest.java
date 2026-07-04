package org.miracum.recruit.tester;

import static org.assertj.core.api.Assertions.assertThat;

import java.net.URI;
import java.time.Duration;
import java.util.Map;
import org.junit.jupiter.api.Test;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.context.runner.ApplicationContextRunner;
import org.springframework.context.annotation.Configuration;

class TesterPropertiesTest {

  private final ApplicationContextRunner contextRunner =
      new ApplicationContextRunner().withUserConfiguration(TestConfig.class);

  @Test
  void properties_withoutOverrides_shouldUseDefaults() {
    contextRunner.run(
        context -> {
          var properties = context.getBean(TesterProperties.class);

          assertThat(properties.mailhogApiBaseUrl())
              .isEqualTo(new URI("http://localhost:8025/api/"));
          assertThat(properties.fhirServerBaseUrl())
              .isEqualTo(new URI("http://localhost:8082/fhir"));
          assertThat(properties.totalDuration()).isEqualTo(Duration.ofSeconds(30));
          assertThat(properties.sendCount()).isEqualTo(5);
          assertThat(properties.expectedNumberOfMessages()).isEqualTo(5);
          assertThat(properties.retries()).isEqualTo(10);
        });
  }

  @Test
  void properties_withCommandLineStyleOverrides_shouldBindThem() {
    contextRunner
        .withPropertyValues(
            "mailhog-api-base-url=http://mailhog.example:8025/api/",
            "fhir-resource-bundle=/tmp/sample-list-bundle.json",
            "total-duration=5m",
            "send-count=10",
            "expected-number-of-messages=2")
        .run(
            context -> {
              var properties = context.getBean(TesterProperties.class);

              assertThat(properties.mailhogApiBaseUrl())
                  .isEqualTo(new URI("http://mailhog.example:8025/api/"));
              assertThat(properties.fhirResourceBundle()).isEqualTo("/tmp/sample-list-bundle.json");
              assertThat(properties.totalDuration()).isEqualTo(Duration.ofMinutes(5));
              assertThat(properties.sendCount()).isEqualTo(10);
              assertThat(properties.expectedNumberOfMessages()).isEqualTo(2);
            });
  }

  @Test
  void properties_withExpectedResourceCounts_shouldBindThemAsMap() {
    contextRunner
        .withPropertyValues(
            "expected-resource-counts.ResearchStudy=1",
            "expected-resource-counts.Patient=100",
            "expected-resource-counts.ResearchSubject=100")
        .run(
            context -> {
              var properties = context.getBean(TesterProperties.class);

              assertThat(properties.expectedResourceCounts())
                  .containsExactlyInAnyOrderEntriesOf(
                      Map.of(
                          "ResearchStudy", 1,
                          "Patient", 100,
                          "ResearchSubject", 100));
            });
  }

  @Configuration
  @EnableConfigurationProperties(TesterProperties.class)
  static class TestConfig {}
}
