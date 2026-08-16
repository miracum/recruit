package org.miracum.recruit.supersetlibrarysync;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.miracum.recruit.supersetlibrarysync.annotation.SqlAnnotationParser;
import org.miracum.recruit.supersetlibrarysync.library.SqlLibraryBuilder;
import org.miracum.recruit.supersetlibrarysync.superset.SavedQuery;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetClient;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetProperties;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

/**
 * Orchestrates one sync cycle: fetch the (tag-filtered) Superset saved queries, turn each one with
 * at least one recognized SQL annotation into a {@code sql-query} {@link Library}, and upsert all
 * of them in one FHIR {@code batch} bundle of conditional {@code PUT}s keyed by an identifier
 * derived from the saved query's Superset id - the {@code batch} bundle type (as opposed to {@code
 * transaction}) is what gives each entry independent, isolated success/failure, since a {@code
 * transaction} bundle would roll back entirely if even one Library failed server-side validation.
 *
 * <p>Failures - both while preparing a Library (e.g. an invalid {@code @status} value) and while
 * submitting it - are isolated per saved query so one bad query doesn't prevent the rest of the
 * batch from syncing, mirroring {@code query-sql-on-fhir}'s {@code PollForStudies}.
 */
@Component
public class SyncService {

  private static final Logger log = LoggerFactory.getLogger(SyncService.class);

  private final SupersetClient supersetClient;
  private final SupersetProperties supersetProperties;
  private final SqlAnnotationParser annotationParser;
  private final SqlLibraryBuilder libraryBuilder;
  private final IGenericClient fhirClient;

  public SyncService(
      SupersetClient supersetClient,
      SupersetProperties supersetProperties,
      SqlAnnotationParser annotationParser,
      SqlLibraryBuilder libraryBuilder,
      IGenericClient fhirClient) {
    this.supersetClient = supersetClient;
    this.supersetProperties = supersetProperties;
    this.annotationParser = annotationParser;
    this.libraryBuilder = libraryBuilder;
    this.fhirClient = fhirClient;
  }

  public SyncResult sync() throws IOException {
    var candidates = fetchCandidates();
    log.info(
        "Found {} saved quer{} to consider syncing",
        candidates.size(),
        candidates.size() == 1 ? "y" : "ies");

    var toSubmit = new ArrayList<Library>();
    var preparationFailures = 0;

    for (var candidate : candidates) {
      try {
        var savedQuery = supersetClient.getSavedQuery(candidate.id());
        var annotations = annotationParser.parse(savedQuery.sql());

        // safety net beyond the tag filter: a saved query that matched the tag but has no
        // recognized annotation at all is almost certainly not meant to become a Library.
        if (annotations.isEmpty()) {
          log.info(
              "Saved query {} ('{}') has no recognized SQL annotations; skipping.",
              savedQuery.id(),
              savedQuery.label());
          continue;
        }

        toSubmit.add(libraryBuilder.build(savedQuery, annotations));
      } catch (Exception ex) {
        preparationFailures++;
        log.error("Failed to prepare a Library for saved query {}", candidate.id(), ex);
      }
    }

    if (toSubmit.isEmpty()) {
      return new SyncResult(candidates.size(), 0, preparationFailures);
    }

    var submissionResult = submit(toSubmit);
    return new SyncResult(
        candidates.size(),
        submissionResult.succeeded(),
        preparationFailures + submissionResult.failed());
  }

  private List<SavedQuery> fetchCandidates() throws IOException {
    var savedQueries = supersetClient.listSavedQueries();

    var tagFilter = supersetProperties.tagFilter();
    if (tagFilter == null || tagFilter.isBlank()) {
      return savedQueries;
    }

    return savedQueries.stream().filter(query -> query.hasTag(tagFilter)).toList();
  }

  private SubmissionResult submit(List<Library> libraries) {
    var bundle = new Bundle().setType(Bundle.BundleType.BATCH);
    for (var library : libraries) {
      var identifier = library.getIdentifierFirstRep();
      var conditionalUrl =
          "Library?identifier=" + identifier.getSystem() + "|" + identifier.getValue();
      bundle
          .addEntry()
          .setResource(library)
          .getRequest()
          .setMethod(Bundle.HTTPVerb.PUT)
          .setUrl(conditionalUrl);
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
