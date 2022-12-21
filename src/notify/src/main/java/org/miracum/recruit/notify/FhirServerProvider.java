package org.miracum.recruit.notify;

import static net.logstash.logback.argument.StructuredArguments.kv;
import static org.hl7.fhir.instance.model.api.IBaseBundle.LINK_NEXT;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.rest.server.exceptions.PreconditionFailedException;
import ca.uhn.fhir.util.BundleUtil;
import java.util.ArrayList;
import java.util.List;
import org.hl7.fhir.instance.model.api.IAnyResource;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.CommunicationRequest;
import org.hl7.fhir.r4.model.CommunicationRequest.CommunicationRequestStatus;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.Practitioner;
import org.hl7.fhir.r4.model.ResearchStudy;
import org.hl7.fhir.r4.model.ResearchSubject;
import org.miracum.recruit.notify.fhirserver.FhirSystemsConfig;
import org.miracum.recruit.notify.practitioner.PractitionerUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;

/** Provide search results from target fhir server. */
@Service
public class FhirServerProvider {
  private static final Logger LOG = LoggerFactory.getLogger(FhirServerProvider.class);

  private final IGenericClient fhirClient;
  private final FhirSystemsConfig fhirSystemsConfig;

  /** Constructor for Fhir Server providing search results. */
  @Autowired
  public FhirServerProvider(IGenericClient fhirClient, FhirSystemsConfig fhirSystemsConfig) {
    this.fhirClient = fhirClient;
    this.fhirSystemsConfig = fhirSystemsConfig;
  }

  /** If previous screening list is available it will be checked if list changed. */
  public ListResource getPreviousScreeningListFromServer(ListResource currentList) {
    var versionId = currentList.getMeta().getVersionId();

    if (versionId == null) {
      LOG.warn("list {} version id is null", currentList.getId());
      return null;
    }

    int lastVersionId = Integer.parseInt(versionId) - 1;
    if (lastVersionId <= 0) {
      return null;
    }

    return fhirClient
        .read()
        .resource(ListResource.class)
        .withIdAndVersion(currentList.getIdElement().getIdPart(), Integer.toString(lastVersionId))
        .execute();
  }

  /** Query all research subjects from list. */
  public List<ResearchSubject> getResearchSubjectsFromList(ListResource list) {
    var listBundle =
        fhirClient
            .search()
            .forResource(ListResource.class)
            .where(IAnyResource.RES_ID.exactly().identifier(list.getId()))
            .include(IBaseResource.INCLUDE_ALL)
            .returnBundle(Bundle.class)
            .execute();

    var researchSubjectList =
        new ArrayList<>(
            BundleUtil.toListOfResourcesOfType(
                fhirClient.getFhirContext(), listBundle, ResearchSubject.class));

    // Load the subsequent pages
    while (listBundle.getLink(LINK_NEXT) != null) {
      listBundle = fhirClient.loadPage().next(listBundle).execute();
      researchSubjectList.addAll(
          BundleUtil.toListOfResourcesOfType(
              fhirClient.getFhirContext(), listBundle, ResearchSubject.class));
    }

    return researchSubjectList;
  }

  /** Query research study resource from target fhir server by given id. */
  public ResearchStudy getResearchStudyFromId(String id) {
    return fhirClient.read().resource(ResearchStudy.class).withId(id).execute();
  }

  /** Query list of practitioners from server by their email address */
  // TODO: refactor to use a single search bundle request instead of multiple search invocations
  public List<Practitioner> getPractitionersByEmail(List<String> subscribers) {
    var practitionerObjects = new ArrayList<Practitioner>();

    for (var subscriberName : subscribers) {
      LOG.debug("fetching FHIR Practitioner {}", kv("subscriberName", subscriberName));

      var listBundlePractitioners =
          fhirClient
              .search()
              .forResource(Practitioner.class)
              .where(Practitioner.EMAIL.exactly().code(subscriberName))
              .returnBundle(Bundle.class)
              .execute();

      var practitionerList =
          BundleUtil.toListOfResourcesOfType(
              fhirClient.getFhirContext(), listBundlePractitioners, Practitioner.class);

      if (practitionerList.isEmpty()) {
        LOG.warn("no Practitioner resource with {} found", kv("email", subscriberName));
        return List.of();
      }

      if (practitionerList.size() > 1) {
        LOG.warn(
            "found more than one practitioner with {}. Returning the first one.",
            kv("email", subscriberName));
      }

      practitionerObjects.add(practitionerList.get(0));
    }

    return practitionerObjects;
  }

  /**
   * Query active CommunicationRequests from FHIR server for the given list of subscriber's email
   * addresses
   */
  // TODO: instead of N x M loops, only fetch the active communication requests for a given
  // subscriber.
  public List<CommunicationRequest> getOpenMessagesForSubscribers(List<String> subscribers) {
    LOG.info("retrieving open messages for {}", kv("numSubscribers", subscribers.size()));

    var allOpenMessages =
        getCommunicationRequestsIncludingRecipientsByStatus(CommunicationRequestStatus.ACTIVE);

    if (allOpenMessages.isEmpty()) {
      LOG.info("no active CommunicationRequest resources found");
      return List.of();
    }

    var messages = new ArrayList<CommunicationRequest>();
    for (var subscriber : subscribers) {
      for (var message : allOpenMessages) {
        var recipientList = message.getRecipient();
        for (var reference : recipientList) {
          if (reference.getResource().fhirType().equals("Practitioner")) {
            var practitioner = (Practitioner) reference.getResource();

            LOG.debug(
                "checking if {} matches with {} {} for {}",
                kv("subscriber", subscriber),
                kv("communicationRequestReason", message.getReasonCodeFirstRep().getText()),
                kv("practitionerEmail", practitioner.getTelecomFirstRep().getValue()),
                kv("message", message.getIdElement().getIdPart()));

            if (PractitionerUtils.hasEmail(practitioner, subscriber)) {
              LOG.debug(
                  "add {} to list for {} ({})",
                  kv("practitioner", practitioner.getIdElement().getIdPart()),
                  kv("message", message.getIdElement().getIdPart()),
                  kv("subscriber", subscriber));
              messages.add(message);
            }
          }
        }
      }
    }

    return messages;
  }

  public List<CommunicationRequest> getCommunicationRequestsIncludingRecipientsByStatus(
      CommunicationRequestStatus status) {
    LOG.info("retrieving CommunicationRequest with {} from server", kv("status", status));

    var results =
        fhirClient
            .search()
            .forResource(CommunicationRequest.class)
            .where(CommunicationRequest.STATUS.exactly().code(status.toCode()))
            .and(
                CommunicationRequest.IDENTIFIER.hasSystemWithAnyCode(
                    fhirSystemsConfig.getCommunication()))
            .include(CommunicationRequest.INCLUDE_RECIPIENT.asNonRecursive())
            .returnBundle(Bundle.class)
            .execute();

    var allMessages = new ArrayList<CommunicationRequest>();

    do {
      // cast all resources in the bundle to CommunicationRequest
      var messagesInPageBundle =
          BundleUtil.toListOfResourcesOfType(
              fhirClient.getFhirContext(), results, CommunicationRequest.class);

      allMessages.addAll(messagesInPageBundle);

      if (results.getLink(LINK_NEXT) != null) {
        LOG.debug(
            "fetching next page of results {} from server",
            kv("link", results.getLink(LINK_NEXT).getUrl()));
        var resultsFinal = results;
        results = fhirClient.loadPage().next(resultsFinal).execute();
      } else {
        results = null;
      }

    } while (results != null);

    return allMessages;
  }

  /**
   * Query communication resources from target fhir server and with given fhir system that are in
   * state active to be delivered.
   */
  public List<CommunicationRequest> getPreparedMessages() {
    return getCommunicationRequestsIncludingRecipientsByStatus(CommunicationRequestStatus.ACTIVE);
  }

  public Bundle executeTransaction(Bundle transaction) {
    return fhirClient.transaction().withBundle(transaction).execute();
  }

  public void executeSingleConditionalCreate(List<Practitioner> practitioners) {
    for (Practitioner practitioner : practitioners) {
      var contactPoint = PractitionerUtils.getFirstEmailFromPractitioner(practitioner);
      if (contactPoint.isPresent()) {
        var email = contactPoint.get().getValue();

        try {

          Bundle bundle = new Bundle();
          bundle.setType(Bundle.BundleType.TRANSACTION);

          bundle
              .addEntry()
              .setFullUrl(practitioner.getIdElement().getValue())
              .setResource(practitioner)
              .getRequest()
              .setUrl("Practitioner")
              .setIfNoneExist(String.format("Practitioner?email=%s", email))
              .setMethod(Bundle.HTTPVerb.POST);

          executeTransaction(bundle);

        } catch (PreconditionFailedException e) {
          LOG.warn(
              "adding practitioners will be skipped because filter by email caused problem",
              kv("practitioneremail", email));
        }
      }
    }
  }
}
