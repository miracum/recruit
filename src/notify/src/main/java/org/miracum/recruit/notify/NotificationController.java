package org.miracum.recruit.notify;

import static net.logstash.logback.argument.StructuredArguments.kv;

import ca.uhn.fhir.parser.IParser;
import com.google.common.base.Strings;
import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.ResearchSubject;
import org.hl7.fhir.r4.model.ResearchSubject.ResearchSubjectStatus;
import org.miracum.recruit.notify.fhirserver.FhirSystemsConfig;
import org.miracum.recruit.notify.message.MessageCreator;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.retry.RetryCallback;
import org.springframework.retry.RetryContext;
import org.springframework.retry.listener.RetryListenerSupport;
import org.springframework.retry.support.RetryTemplate;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/**
 * Define endpoint for receiving PUT request from fhir server sending subscriptions for recruiting
 * list.
 */
@RestController
public class NotificationController {
  private static final Logger LOG = LoggerFactory.getLogger(NotificationController.class);

  private final RetryTemplate retryTemplate;
  private final MessageCreator messageCreator;
  private final IParser fhirParser;
  private final FhirServerProvider fhirServer;
  private final FhirSystemsConfig fhirSystems;

  /**
   * Prepare config items and email utils for receiving and handle subscription events from target
   * fhir server.
   */
  @Autowired
  public NotificationController(
      RetryTemplate retryTemplate,
      FhirSystemsConfig fhirSystems,
      FhirServerProvider fhirServer,
      MessageCreator messageCreator,
      IParser fhirParser) {
    this.retryTemplate = retryTemplate;
    this.fhirSystems = fhirSystems;
    this.fhirServer = fhirServer;
    this.messageCreator = messageCreator;
    this.fhirParser = fhirParser;
  }

  /**
   * Expose endpoint that will be assigned to subscription and accepts application/fhir+json
   * content.
   */
  @PutMapping(value = "/on-list-change/List/{id}", consumes = "application/fhir+json")
  public void onListChange(
      @PathVariable(value = "id") String resourceId, @RequestBody String body) {
    LOG.info("onListChange invoked for {}", kv("list", resourceId));

    if (body == null) {
      LOG.error("request body is null");
      return;
    }

    var list = fhirParser.parseResource(ListResource.class, body);

    if (!list.hasEntry()) {
      LOG.warn("Received empty screening list {}, aborting.", list.getId());
      return;
    }

    retryTemplate.registerListener(
        new RetryListenerSupport() {
          @Override
          public <T, E extends Throwable> void onError(
              RetryContext context, RetryCallback<T, E> callback, Throwable throwable) {
            LOG.warn("handleSubscription failed. {} attempt.", context.getRetryCount());
          }
        });

    retryTemplate.execute(retryContext -> handleSubscription(list));
  }

  private Void handleSubscription(ListResource list) {
    var studyReferenceExtension = list.getExtensionByUrl(fhirSystems.getScreeningListReference());

    if (studyReferenceExtension == null) {
      LOG.warn(
          "studyReferenceExtension not set for {}. Impossible to determine receiver, aborting.",
          list.getId());
      return null;
    }

    MDC.put("list", list.getId());

    if (!hasPatientListChanged(list)) {
      LOG.info("list hasn't changed since last time");
      return null;
    }

    var studyReference = (Reference) studyReferenceExtension.getValue();

    var researchSubjectList = fhirServer.getResearchSubjectsFromList(list);
    if (!hasPatientListAnyCandidates(researchSubjectList)) {
      LOG.info("list doesn't contain any subjects with status 'candidate'");
      return null;
    }

    final var acronym = retrieveStudyAcronym(studyReference);
    if (Strings.isNullOrEmpty(acronym)) {
      LOG.error("couldn't get acronym from list");
      return null;
    }

    var listId = list.getIdElement().getIdPart();
    messageCreator.temporaryStoreMessagesInFhir(acronym, listId);

    return null;
  }

  private String retrieveStudyAcronym(Reference studyReference) {
    var studyAcronym = "";

    if (studyReference.hasDisplay()) {
      studyAcronym = studyReference.getDisplay();
    } else {
      var study =
          fhirServer.getResearchStudyFromId(studyReference.getReferenceElement().getIdPart());
      var studyArg = kv("study", studyReference.getReference());

      if (study.hasExtension(fhirSystems.getStudyAcronym())) {
        var studyAcronymExtension = study.getExtensionByUrl(fhirSystems.getStudyAcronym());
        studyAcronym = studyAcronymExtension.getValue().toString();
        LOG.debug(
            "using {} from extension as study identifier for {}.",
            kv("acronym", studyAcronym),
            studyArg);
      } else {
        LOG.warn("study acronym not set for {}.", studyArg);
        if (study.hasTitle()) {
          studyAcronym = study.getTitle();
          LOG.debug("Using {} as study identifier for {}.", kv("title", studyAcronym), studyArg);
        } else {
          LOG.error("No identifier available for {}. Aborting.", studyArg);
          return null;
        }
      }
    }
    return studyAcronym;
  }

  private boolean hasPatientListAnyCandidates(List<ResearchSubject> researchSubjects) {
    return researchSubjects.stream()
        .anyMatch(subject -> subject.getStatus() == ResearchSubjectStatus.CANDIDATE);
  }

  private boolean hasPatientListChanged(ListResource newScreenList) {
    var lastScreenList = fhirServer.getPreviousScreeningListFromServer(newScreenList);
    if (lastScreenList == null) {
      return true;
    }

    var newResearchSubjectIds = getResearchSubjectIds(newScreenList.getEntry());
    var lastResearchSubjectIds = getResearchSubjectIds(lastScreenList.getEntry());
    return !newResearchSubjectIds.equals(lastResearchSubjectIds);
  }

  private Set<String> getResearchSubjectIds(List<ListResource.ListEntryComponent> entry) {
    return entry.stream()
        .map(item -> item.getItem().getReferenceElement().getIdPart())
        .collect(Collectors.toSet());
  }
}
