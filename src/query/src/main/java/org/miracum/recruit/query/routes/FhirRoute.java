package org.miracum.recruit.query.routes;

import static net.logstash.logback.argument.StructuredArguments.kv;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.model.api.Include;
import ca.uhn.fhir.parser.IParser;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import ca.uhn.fhir.util.BundleUtil;
import java.util.List;
import java.util.Optional;
import org.apache.camel.LoggingLevel;
import org.apache.camel.builder.RouteBuilder;
import org.hl7.fhir.instance.model.api.IBaseBundle;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.Patient;
import org.hl7.fhir.r4.model.ResearchSubject;
import org.miracum.recruit.query.FhirCohortTransactionBuilder;
import org.miracum.recruit.query.LabelExtractor;
import org.miracum.recruit.query.ScreeningListResources;
import org.miracum.recruit.query.models.CohortDefinition;
import org.miracum.recruit.query.models.Person;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

@Component
public class FhirRoute extends RouteBuilder {

  static final String CREATE_SCREENING_LIST = "direct:fhir.createScreeningList";

  private static final String FORCE_OVERWRITE_LABEL = "overwrite-existing-screening-list";

  private final FhirCohortTransactionBuilder fhirBuilder;
  private final IParser fhirParser;
  private final IGenericClient fhirClient;
  private final FhirContext fhirContext;
  private final boolean shouldAppendToExistingList;
  private final LabelExtractor labelExtractor;

  @Autowired
  public FhirRoute(
      FhirCohortTransactionBuilder fhirBuilder,
      FhirContext fhirContext,
      IGenericClient fhirClient,
      @Value("${query.append-recommendations-to-existing-list}") boolean shouldAppendToExistingList,
      LabelExtractor labelExtractor) {
    this.fhirContext = fhirContext;
    this.fhirBuilder = fhirBuilder;
    this.fhirParser = fhirContext.newJsonParser().setPrettyPrint(true);
    this.fhirClient = fhirClient;
    this.shouldAppendToExistingList = shouldAppendToExistingList;
    this.labelExtractor = labelExtractor;
  }

  @Override
  public void configure() {
    // Gets the Ids of the patients for one cohort in "body" and CohortDefinition in "header.cohort"
    from(CREATE_SCREENING_LIST)
        .log(
            LoggingLevel.INFO,
            log,
            "[Cohort ${header.cohort.id}] adding ${body.size()} patient(s) for cohort '${header.cohort.id} - ${header.cohort.name}'")
        .process(
            ex -> {
              // get data from omop db and save it in variables
              @SuppressWarnings("unchecked")
              var patients = (List<Person>) ex.getIn().getBody();
              var cohortDefinition = (CohortDefinition) ex.getIn().getHeader("cohort");
              var cohortSize = (long) ex.getIn().getHeader("cohortSize");

              // check if either the name or the description contains the hard-coded
              // `overwrite-existing-screening-list` label, which causes the recommendations
              // to be overwritten regardless of any other config.
              var forceOverwriteExistingList =
                  labelExtractor.hasLabel(cohortDefinition.getDescription(), FORCE_OVERWRITE_LABEL)
                      || labelExtractor.hasLabel(cohortDefinition.getName(), FORCE_OVERWRITE_LABEL);

              // if the module is configured to append to existing screening lists,
              // first try fetching this List from the server
              Bundle transaction;
              if (forceOverwriteExistingList || !shouldAppendToExistingList) {
                transaction =
                    fhirBuilder.buildFromOmopCohort(cohortDefinition, patients, cohortSize);
              } else {
                var previousScreeningListResources =
                    fetchPatientsInCohort(cohortDefinition.getId());

                if (previousScreeningListResources.isPresent()) {
                  transaction =
                      fhirBuilder.buildFromOmopCohort(
                          cohortDefinition,
                          patients,
                          cohortSize,
                          previousScreeningListResources.get());
                } else {
                  transaction =
                      fhirBuilder.buildFromOmopCohort(cohortDefinition, patients, cohortSize);
                }
              }

              log.debug(fhirParser.encodeResourceToString(transaction));
              // set bundle as http body
              ex.getIn().setBody(transaction);
            })
        .to(
            "fhir:transaction/withBundle?serverUrl={{fhir.url}}&inBody=bundle&fhirVersion=R4&fhirContext=#bean:fhirContext")
        .process(
            ex -> {
              var response = (Bundle) ex.getIn().getBody();
              log.debug(fhirParser.encodeResourceToString(response));
            })
        .log(LoggingLevel.INFO, log, "[Cohort ${header.cohort.id}] processing finished");
  }

  private Optional<ScreeningListResources> fetchPatientsInCohort(Long cohortId) {
    var screeningListIdentifier = fhirBuilder.getScreeningListIdentifierFromCohortId(cohortId);

    // we need both the previous List resource (if available) and all ResearchSubjects + Patients:
    // the List is required to be able to retain the List.entry.date from the previous list when
    // appending the newly found persons from this cohort generation run.
    // the Patient resources are used to determine which patients were newly found in this run
    // compared to the last
    // note that we eventually return only the Patient resources included in this search,
    // this is because the ResearchSubjects don't contain any identifier we could use
    // to determine whether a patient we plan on adding to the new list already exists
    // or has to be added. This identifier is the Patient.identifier, ie. the medical record number.

    // fhir/List?identifier=<...>/screeningListId%7Cscreeninglist-3
    // &_include=List:item
    // &_include:iterate=ResearchSubject:patient
    var searchResults =
        fhirClient
            .search()
            .forResource(ListResource.class)
            .where(
                ListResource.IDENTIFIER
                    .exactly()
                    .systemAndIdentifier(
                        screeningListIdentifier.getSystem(), screeningListIdentifier.getValue()))
            .include(new Include("List:item"))
            .include(new Include("ResearchSubject:patient", true))
            .returnBundle(Bundle.class)
            .execute();

    var listResources =
        BundleUtil.toListOfResourcesOfType(fhirContext, searchResults, ListResource.class);

    if (listResources.isEmpty()) {
      return Optional.empty();
    }

    if (listResources.size() > 1) {
      log.warn(
          "Found more than one List resource (actually {}) for {}. Defaulting to the first occurrence.",
          kv("numLists", listResources.size()),
          kv("cohortId", cohortId));
    }

    var previousList = listResources.get(0);

    var foundSubjects =
        BundleUtil.toListOfResourcesOfType(fhirContext, searchResults, ResearchSubject.class);

    var foundPatients =
        BundleUtil.toListOfResourcesOfType(fhirContext, searchResults, Patient.class);

    // Load the subsequent pages
    while (searchResults.getLink(IBaseBundle.LINK_NEXT) != null) {
      searchResults = fhirClient.loadPage().next(searchResults).execute();

      foundSubjects.addAll(
          BundleUtil.toListOfResourcesOfType(fhirContext, searchResults, ResearchSubject.class));
      foundPatients.addAll(
          BundleUtil.toListOfResourcesOfType(fhirContext, searchResults, Patient.class));
    }

    log.debug(
        "Found a total of {} List, {} ResearchSubject and {} Patient resources for {}",
        kv("numLists", listResources.size()),
        kv("numResearchSubjects", foundSubjects.size()),
        kv("numPatients", foundPatients.size()),
        kv("cohortId", cohortId));

    if (foundPatients.isEmpty()) {
      return Optional.empty();
    }

    if (foundPatients.size() != foundSubjects.size()) {
      log.error(
          "The number of ResearchSubject resources ({}) differs from the number of Patient ones ({})",
          kv("numResearchSubjects", foundSubjects.size()),
          kv("numPatients", foundPatients.size()));
    }

    var screeningListResources =
        new ScreeningListResources(previousList, foundPatients, foundSubjects);

    return Optional.of(screeningListResources);
  }
}
