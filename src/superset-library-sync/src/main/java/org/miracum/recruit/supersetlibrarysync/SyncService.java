package org.miracum.recruit.supersetlibrarysync;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.util.ArrayList;
import java.util.List;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.miracum.recruit.supersetlibrarysync.annotation.SqlAnnotationParser;
import org.miracum.recruit.supersetlibrarysync.library.SqlLibraryBuilder;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetSavedQueryRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

/**
 * Orchestrates one sync cycle: read every Superset saved query, turn each one with at least one
 * recognized SQL annotation into a {@code sql-query} {@link Library}, and upsert all of them in one
 * FHIR {@code batch} bundle of {@code PUT}s keyed by each Library's own id - a deterministic hash
 * of its identifier computed by {@code SqlLibraryBuilder} via {@code IdUtils.fromIdentifier} - the
 * {@code batch} bundle type (as opposed to {@code transaction}) is what gives each entry
 * independent, isolated success/failure, since a {@code transaction} bundle would roll back
 * entirely if even one Library failed server-side validation.
 *
 * <p>Failures - both while preparing a Library (e.g. an invalid {@code @status} value) and while
 * submitting it - are isolated per saved query so one bad query doesn't prevent the rest of the
 * batch from syncing, mirroring {@code query-sql-on-fhir}'s {@code PollForStudies}.
 */
@Component
public class SyncService {

  private static final Logger log = LoggerFactory.getLogger(SyncService.class);

  private final SupersetSavedQueryRepository savedQueryRepository;
  private final SyncProperties syncProperties;
  private final SqlAnnotationParser annotationParser;
  private final SqlLibraryBuilder libraryBuilder;
  private final IGenericClient fhirClient;
  private final FhirContext fhirContext;

  public SyncService(
      SupersetSavedQueryRepository savedQueryRepository,
      SyncProperties syncProperties,
      SqlAnnotationParser annotationParser,
      SqlLibraryBuilder libraryBuilder,
      IGenericClient fhirClient,
      FhirContext fhirContext) {
    this.savedQueryRepository = savedQueryRepository;
    this.syncProperties = syncProperties;
    this.annotationParser = annotationParser;
    this.libraryBuilder = libraryBuilder;
    this.fhirClient = fhirClient;
    this.fhirContext = fhirContext;
  }

  public SyncResult sync() {
    var savedQueries = savedQueryRepository.findAll();
    log.info(
        "Found {} saved quer{} to consider syncing",
        savedQueries.size(),
        savedQueries.size() == 1 ? "y" : "ies");

    var toSubmit = new ArrayList<Library>();
    var preparationFailures = 0;

    for (var savedQuery : savedQueries) {
      try {
        var annotations = annotationParser.parse(savedQuery.sql());

        // safety net: a saved query with no recognized annotation at all is almost certainly not
        // meant to become a Library - this is what actually selects "the ones with annotations
        // set" out of every saved query on the instance, now that there's no REST tag filter
        // narrowing the candidates beforehand.
        if (annotations.isEmpty()) {
          log.info(
              "Saved query {} ('{}') has no recognized SQL annotations; skipping.",
              savedQuery.id(),
              savedQuery.label());
          continue;
        }

        // @name (rather than the saved query's own id) is what SqlLibraryBuilder derives the
        // Library's identifier and id from, so a saved query without one is left out entirely -
        // logged loudly, not silently skipped like the no-annotations-at-all case above - so it
        // can be given a @name and picked up on a later sync.
        if (annotations.name() == null) {
          log.warn(
              "Saved query {} ('{}') has recognized SQL annotations but no @name; skipping it"
                  + " until one is added.",
              savedQuery.id(),
              savedQuery.label());
          continue;
        }

        toSubmit.add(libraryBuilder.build(savedQuery, annotations));
      } catch (Exception ex) {
        preparationFailures++;
        log.error("Failed to prepare a Library for saved query {}", savedQuery.id(), ex);
      }
    }

    if (toSubmit.isEmpty()) {
      return new SyncResult(savedQueries.size(), 0, preparationFailures);
    }

    if (syncProperties.dryRun()) {
      logDryRun(toSubmit);
      return new SyncResult(savedQueries.size(), toSubmit.size(), preparationFailures);
    }

    var submissionResult = submit(toSubmit);
    return new SyncResult(
        savedQueries.size(),
        submissionResult.succeeded(),
        preparationFailures + submissionResult.failed());
  }

  private void logDryRun(List<Library> libraries) {
    log.info(
        "Dry-run enabled: {} Library resource(s) would be synced, not submitting them to the"
            + " FHIR server.",
        libraries.size());

    var jsonParser = fhirContext.newJsonParser().setPrettyPrint(true);
    for (var library : libraries) {
      log.info("Would upsert Library:\n{}", jsonParser.encodeResourceToString(library));
    }
  }

  private SubmissionResult submit(List<Library> libraries) {
    var bundle = new Bundle().setType(Bundle.BundleType.BATCH);
    for (var library : libraries) {
      bundle
          .addEntry()
          .setResource(library)
          .getRequest()
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl("Library/" + library.getIdElement().getIdPart());
    }

    Bundle response;
    try {
      response = fhirClient.transaction().withBundle(bundle).execute();
    } catch (Exception ex) {
      log.error(
          "Failed to submit the batch of {} Library resource(s) to the FHIR server",
          libraries.size(),
          ex);
      return new SubmissionResult(0, libraries.size());
    }

    var succeeded = 0;
    var failed = 0;
    for (var entry : response.getEntry()) {
      var status = entry.getResponse().getStatus();
      if (status != null && status.startsWith("2")) {
        succeeded++;
      } else {
        failed++;
        log.error("Failed to upsert a Library: {}", status);
      }
    }

    return new SubmissionResult(succeeded, failed);
  }

  private record SubmissionResult(int succeeded, int failed) {}
}
