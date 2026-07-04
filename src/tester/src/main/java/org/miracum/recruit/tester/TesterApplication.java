package org.miracum.recruit.tester;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.api.SummaryEnum;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.client.exceptions.FhirClientConnectionException;
import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import org.hl7.fhir.r4.model.Bundle;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;

@SpringBootApplication
@ConfigurationPropertiesScan
public class TesterApplication implements ApplicationRunner {

  private static final Logger log = LoggerFactory.getLogger(TesterApplication.class);

  private final TesterProperties properties;
  private final MailHogClient mailHogClient;
  private final FhirContext fhirContext;
  private final IGenericClient fhirClient;

  public TesterApplication(
      TesterProperties properties,
      MailHogClient mailHogClient,
      FhirContext fhirContext,
      IGenericClient fhirClient) {
    this.properties = properties;
    this.mailHogClient = mailHogClient;
    this.fhirContext = fhirContext;
    this.fhirClient = fhirClient;
  }

  public static void main(String[] args) {
    new SpringApplicationBuilder(TesterApplication.class).web(WebApplicationType.NONE).run(args);
  }

  @Override
  public void run(ApplicationArguments args) throws Exception {
    var commands = args.getNonOptionArgs();
    if (commands.size() != 1) {
      throw new IllegalArgumentException(
          "Expected exactly one command (delete-messages, test, assert, or "
              + "assert-fhir-resource-counts), got: "
              + commands);
    }

    switch (commands.get(0)) {
      case "delete-messages" -> runDeleteMessages();
      case "test" -> runTest();
      case "assert" -> runAssert();
      case "assert-fhir-resource-counts" -> runAssertFhirResourceCounts();
      default -> throw new IllegalArgumentException("Unknown command: " + commands.get(0));
    }
  }

  private void runDeleteMessages() throws IOException, InterruptedException {
    log.info("Deleting all previous messages");

    var messages = mailHogClient.getMessages(properties.mailhogApiBaseUrl());
    log.info("A total of {} messages already on the server.", messages.total());

    mailHogClient.deleteMessages(properties.mailhogApiBaseUrl());

    log.info("Done.");
  }

  private void runTest() throws IOException, InterruptedException {
    var fhirResourceBundle = Path.of(properties.fhirResourceBundle());
    var totalDuration = properties.totalDuration();
    var sendCount = properties.sendCount();

    log.info(
        "Sending FHIR bundle from file {} to server {} {} times over a duration of {} ({}s)",
        fhirResourceBundle.toAbsolutePath(),
        properties.fhirServerBaseUrl(),
        sendCount,
        totalDuration,
        totalDuration.toSeconds());

    var bundleJson = Files.readString(fhirResourceBundle);
    var bundle = fhirContext.newJsonParser().parseResource(Bundle.class, bundleJson);

    var sleepBetween = totalDuration.dividedBy(sendCount);

    for (var i = 0; i < sendCount; i++) {
      log.info("Sending for the {}. time", i);

      sendWithRetry(bundle);

      log.info("Sleeping for {}", sleepBetween);
      Thread.sleep(sleepBetween);
    }
  }

  private void sendWithRetry(Bundle bundle) throws InterruptedException {
    final var maxAttempts = 3;

    for (var attempt = 1; attempt < maxAttempts; attempt++) {
      try {
        fhirClient.transaction().withBundle(bundle).execute();
        return;
      } catch (FhirClientConnectionException exc) {
        log.warn("Failed to send the bundle. Attempt: {}", attempt, exc);
        Thread.sleep(Duration.ofSeconds((long) Math.pow(2, attempt)));
      }
    }

    fhirClient.transaction().withBundle(bundle).execute();
  }

  private void runAssert() throws InterruptedException {
    log.info("Using MailHog base URL: {}", properties.mailhogApiBaseUrl());

    var expectedMessageCount = properties.expectedNumberOfMessages();
    var retries = properties.retries();

    for (var attempt = 1; attempt <= retries; attempt++) {
      MailHogClient.MailHogMessages response;
      try {
        response = mailHogClient.getMessages(properties.mailhogApiBaseUrl());
      } catch (IOException | InterruptedException exc) {
        log.warn("Failed to get response from MailHog: {}. Attempt: {}", exc.getMessage(), attempt);
        if (attempt == retries) {
          throw new IllegalStateException("Failed to get response from MailHog", exc);
        }

        Thread.sleep(Duration.ofMinutes(1));
        continue;
      }

      log.info(
          "Expected message count is {}. Actual: {}. Attempt: {}",
          expectedMessageCount,
          response.total(),
          attempt);

      if (expectedMessageCount == response.total()) {
        log.info("Found expected message count!");
        return;
      }

      if (attempt == retries) {
        throw new IllegalStateException(
            "response.total (%d) is not the expected %d after %d attempts."
                .formatted(response.total(), expectedMessageCount, attempt));
      }

      Thread.sleep(Duration.ofMinutes(1));
    }
  }

  private void runAssertFhirResourceCounts() throws InterruptedException {
    var expectedCounts = properties.expectedResourceCounts();
    if (expectedCounts == null || expectedCounts.isEmpty()) {
      throw new IllegalArgumentException(
          "No expected resource counts configured. Use "
              + "--expected-resource-counts.<ResourceType>=<count>, e.g. "
              + "--expected-resource-counts.Patient=100");
    }

    waitForFhirServerUp();

    var failures = new ArrayList<String>();
    for (var entry : expectedCounts.entrySet()) {
      try {
        assertResourceCountWithRetry(entry.getKey(), entry.getValue());
      } catch (IllegalStateException exc) {
        failures.add(exc.getMessage());
      }
    }

    if (!failures.isEmpty()) {
      throw new IllegalStateException(
          "Some resource counts did not match the expected value:\n" + String.join("\n", failures));
    }
  }

  private void waitForFhirServerUp() throws InterruptedException {
    var httpClient = HttpClient.newHttpClient();
    var metadataUrl = URI.create(properties.fhirServerBaseUrl() + "/metadata");
    final var maxAttempts = 15;
    var backoff = Duration.ofSeconds(5);
    final var maxBackoff = Duration.ofSeconds(60);

    log.info("Using FHIR server @ {}", properties.fhirServerBaseUrl());

    for (var attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        var request = HttpRequest.newBuilder(metadataUrl).GET().build();
        var response = httpClient.send(request, HttpResponse.BodyHandlers.discarding());
        if (response.statusCode() == 200) {
          return;
        }

        log.warn(
            "FHIR server responded with status {}. Attempt: {}/{}",
            response.statusCode(),
            attempt,
            maxAttempts);
      } catch (IOException exc) {
        log.warn(
            "Failed to reach the FHIR server: {}. Attempt: {}/{}",
            exc.getMessage(),
            attempt,
            maxAttempts);
      }

      if (attempt == maxAttempts) {
        throw new IllegalStateException("Failed to wait for the FHIR server to be up.");
      }

      Thread.sleep(backoff);
      backoff =
          backoff.multipliedBy(2).compareTo(maxBackoff) > 0 ? maxBackoff : backoff.multipliedBy(2);
    }
  }

  private void assertResourceCountWithRetry(String resourceType, int expectedCount)
      throws InterruptedException {
    var deadline = Instant.now().plus(Duration.ofSeconds(120));
    var backoff = Duration.ofSeconds(1);
    final var maxBackoff = Duration.ofSeconds(10);

    while (true) {
      try {
        var bundle =
            fhirClient
                .search()
                .forResource(resourceType)
                .summaryMode(SummaryEnum.COUNT)
                .returnBundle(Bundle.class)
                .execute();
        var actualCount = bundle.getTotal();

        log.info("{}: expected {}, actual {}", resourceType, expectedCount, actualCount);

        if (actualCount == expectedCount) {
          return;
        }

        if (Instant.now().isAfter(deadline)) {
          throw new IllegalStateException(
              "%s: expected count %d but got %d"
                  .formatted(resourceType, expectedCount, actualCount));
        }
      } catch (FhirClientConnectionException exc) {
        if (Instant.now().isAfter(deadline)) {
          throw new IllegalStateException("Failed to query " + resourceType, exc);
        }

        log.warn("Failed to query {}: {}", resourceType, exc.getMessage());
      }

      Thread.sleep(backoff);
      backoff =
          backoff.multipliedBy(2).compareTo(maxBackoff) > 0 ? maxBackoff : backoff.multipliedBy(2);
    }
  }
}
