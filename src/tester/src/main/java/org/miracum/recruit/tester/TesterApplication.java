package org.miracum.recruit.tester;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.client.exceptions.FhirClientConnectionException;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
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
          "Expected exactly one command (delete-messages, test, or assert), got: " + commands);
    }

    switch (commands.get(0)) {
      case "delete-messages" -> runDeleteMessages();
      case "test" -> runTest();
      case "assert" -> runAssert();
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

    for (var attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        fhirClient.transaction().withBundle(bundle).execute();
        return;
      } catch (FhirClientConnectionException exc) {
        if (attempt == maxAttempts) {
          throw exc;
        }

        log.warn("Failed to send the bundle. Attempt: {}", attempt, exc);
        Thread.sleep(Duration.ofSeconds((long) Math.pow(2, attempt)));
      }
    }
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
}
