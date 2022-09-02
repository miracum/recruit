package org.miracum.recruit.notify;

import ca.uhn.fhir.rest.api.MethodOutcome;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.net.MalformedURLException;
import java.net.URISyntaxException;
import java.net.URL;
import java.util.List;
import javax.annotation.PostConstruct;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.CommunicationRequest;
import org.hl7.fhir.r4.model.Practitioner;
import org.hl7.fhir.r4.model.Subscription;
import org.miracum.recruit.notify.fhirserver.PractitionerTransmitter;
import org.miracum.recruit.notify.mailconfig.UserConfig;
import org.miracum.recruit.notify.practitioner.PractitionerCreator;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.retry.RetryCallback;
import org.springframework.retry.RetryContext;
import org.springframework.retry.listener.RetryListenerSupport;
import org.springframework.retry.support.RetryTemplate;
import org.springframework.stereotype.Component;

/**
 * Startup routine to print config to log, init subscription and practitioner in target fhir server
 * and print message to log when error messages are available in target fhir server.
 */
@Component
public class Startup {

  private static final Logger LOG = LoggerFactory.getLogger(Startup.class);

  private final URL webhookEndpoint;
  private final RetryTemplate retryTemplate;
  private final String criteria;
  private final UserConfig userConfig;
  private final PractitionerTransmitter practitionerTransmitter;
  private final PractitionerCreator practitionerCreator;
  private final FhirServerProvider fhirServerProvider;
  private final IGenericClient fhirClient;

  /**
   * Create util items needed for startup routine to add initial items to target fhir server and
   * prepare for receiving subscriptions.
   */
  @Autowired
  public Startup(
      RetryTemplate retryTemplate,
      @Value("${webhook.endpoint}") URL webhookEndpoint,
      FhirServerProvider fhirServerProvider,
      PractitionerCreator practitionerCreator,
      PractitionerTransmitter practitionerTransmitter,
      IGenericClient fhirClient,
      UserConfig userConfig,
      @Value("${fhir.subscription.criteria}") String criteria)
      throws MalformedURLException, URISyntaxException {

    this.retryTemplate = retryTemplate;

    this.webhookEndpoint = createWebhookEndpoint(webhookEndpoint);

    this.fhirServerProvider = fhirServerProvider;
    this.practitionerCreator = practitionerCreator;
    this.practitionerTransmitter = practitionerTransmitter;
    this.fhirClient = fhirClient;
    this.userConfig = userConfig;
    this.criteria = criteria;

    createWebhookEndpoint(webhookEndpoint);
  }

  private URL createWebhookEndpoint(URL webhookEndpoint)
      throws MalformedURLException, URISyntaxException {
    if (!webhookEndpoint.getPath().endsWith("/on-list-change")) {
      LOG.warn(
          "Specified Webhook endpoint didn't end with '/on-list-change' in path ({}). Appending.",
          webhookEndpoint);
      var path = webhookEndpoint.getPath();
      path += "/on-list-change";
      webhookEndpoint = new URL(webhookEndpoint, path).toURI().normalize().toURL();
    }

    return webhookEndpoint;
  }

  @PostConstruct
  private void init() {
    LOG.info("Using notification config: {}", userConfig);

    retryListenerSupport();
    createSubscription(retryTemplate);
    loadReceiverList(retryTemplate);

    informAboutMessagesInErrorState();
  }

  private void informAboutMessagesInErrorState() {
    List<CommunicationRequest> errorMessages = fhirServerProvider.getErrorMessages();

    for (CommunicationRequest messageInErrorState : errorMessages) {
      LOG.warn(
          "communication resource in error state: {}, please reset manually to \"active\"",
          messageInErrorState.getIdElement().getIdPart());
    }
  }

  private void retryListenerSupport() {
    retryTemplate.registerListener(
        new RetryListenerSupport() {
          @Override
          public <T, E extends Throwable> void onError(
              RetryContext context, RetryCallback<T, E> callback, Throwable throwable) {
            LOG.warn(
                "Trying to connect to FHIR server caused '{}'. Attempt {}",
                throwable.getMessage(),
                context.getRetryCount());
          }
        });
  }

  private void createSubscription(RetryTemplate retryTemplate) {
    LOG.info(
        "Creating subscription resource with criteria '{}' and webhook URL '{}' @ '{}'",
        criteria,
        webhookEndpoint,
        fhirClient.getServerBase());

    var outcome = retryTemplate.execute(retryContext -> createSubscription());
    LOG.info("Subscription resource '{}' created!!", outcome.getId());
  }

  private MethodOutcome createSubscription() {
    var channel =
        new Subscription.SubscriptionChannelComponent()
            .setType(Subscription.SubscriptionChannelType.RESTHOOK)
            .setEndpoint(webhookEndpoint.toString())
            .setPayload("application/fhir+json");

    var subscription =
        new Subscription()
            .setCriteria(criteria)
            .setChannel(channel)
            .setReason("Create notifications based on screening list changes.")
            .setStatus(Subscription.SubscriptionStatus.REQUESTED);

    return fhirClient
        .update()
        .resource(subscription)
        .conditional()
        .where(Subscription.CRITERIA.matchesExactly().value(criteria))
        .execute();
  }

  private void loadReceiverList(RetryTemplate retryTemplate) {
    retryTemplate.execute(retryContext -> createPractitionerListInFhir());
  }

  private Bundle createPractitionerListInFhir() {
    LOG.debug("list of practitioners will now be created");
    var practitioners = practitionerCreator.create();
    return transmitPractitionerToFhir(practitioners);
  }

  private Bundle transmitPractitionerToFhir(List<Practitioner> practitioners) {
    LOG.debug("list of practitioners will now be transmitted");
    return practitionerTransmitter.transmit(practitioners);
  }
}
