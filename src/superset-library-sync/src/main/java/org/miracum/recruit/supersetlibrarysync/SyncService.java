package org.miracum.recruit.supersetlibrarysync;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.miracum.recruit.supersetlibrarysync.annotation.SqlAnnotationParser;
import org.miracum.recruit.supersetlibrarysync.library.SqlLibraryBuilder;
import org.miracum.recruit.supersetlibrarysync.messaging.KafkaLibraryPublisher;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetSavedQueryRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

/**
 * Orchestrates one sync cycle: read every Superset saved query, turn each one with at least one
 * recognized SQL annotation into a {@code sql-query} {@link Library} wrapped in its own
 * single-entry PUT {@link Bundle} (see {@code SqlLibraryBuilder#build}), and deliver every one of
 * them to exactly one destination. When {@link KafkaLibraryPublisher} is present (see {@code
 * sync.kafka.enabled}), each per-Library bundle is published to its configured Kafka topic
 * <em>instead of</em> being submitted to the FHIR server. Otherwise, all of them are upserted in
 * one FHIR {@code batch} bundle assembled from those per-Library entries - the {@code batch} bundle
 * type (as opposed to {@code transaction}) is what gives each entry independent, isolated
 * success/failure, since a {@code transaction} bundle would roll back entirely if even one Library
 * failed server-side validation.
 *
 * <p>Failures - both while preparing a Library (e.g. an invalid {@code @status} value) and while
 * delivering it - are isolated per saved query so one bad query doesn't prevent the rest of the
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
  private final Optional<KafkaLibraryPublisher> kafkaPublisher;

  public SyncService(
      SupersetSavedQueryRepository savedQueryRepository,
      SyncProperties syncProperties,
      SqlAnnotationParser annotationParser,
      SqlLibraryBuilder libraryBuilder,
      IGenericClient fhirClient,
      FhirContext fhirContext,
      Optional<KafkaLibraryPublisher> kafkaPublisher) {
    this.savedQueryRepository = savedQueryRepository;
    this.syncProperties = syncProperties;
    this.annotationParser = annotationParser;
    this.libraryBuilder = libraryBuilder;
    this.fhirClient = fhirClient;
    this.fhirContext = fhirContext;
    this.kafkaPublisher = kafkaPublisher;
  }

  public SyncResult sync() {
    var savedQueries = savedQueryRepository.findAll();
    log.info(
        "Found {} saved quer{} to consider syncing",
        savedQueries.size(),
        savedQueries.size() == 1 ? "y" : "ies");

    var toSubmit = new ArrayList<Bundle>();
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

    var deliveryResult =
        kafkaPublisher.isPresent()
            ? publishToKafka(kafkaPublisher.get(), toSubmit)
            : submit(toSubmit);
    return new SyncResult(
        savedQueries.size(),
        deliveryResult.succeeded(),
        preparationFailures + deliveryResult.failed());
  }

  private void logDryRun(List<Bundle> libraryBundles) {
    log.info(
        "Dry-run enabled: {} Library resource(s) would be synced to {}, not actually sent.",
        libraryBundles.size(),
        kafkaPublisher.isPresent() ? "Kafka" : "the FHIR server");

    var jsonParser = fhirContext.newJsonParser().setPrettyPrint(true);
    for (var libraryBundle : libraryBundles) {
      log.info("Would upsert Library:\n{}", jsonParser.encodeResourceToString(libraryBundle));
    }
  }

  private DeliveryResult publishToKafka(
      KafkaLibraryPublisher publisher, List<Bundle> libraryBundles) {
    var succeeded = 0;
    var failed = 0;
    for (var libraryBundle : libraryBundles) {
      if (publisher.publish(libraryBundle)) {
        succeeded++;
      } else {
        failed++;
      }
    }
    return new DeliveryResult(succeeded, failed);
  }

  private DeliveryResult submit(List<Bundle> libraryBundles) {
    var bundle = new Bundle().setType(Bundle.BundleType.BATCH);
    for (var libraryBundle : libraryBundles) {
      bundle.getEntry().addAll(libraryBundle.getEntry());
    }

    Bundle response;
    try {
      response = fhirClient.transaction().withBundle(bundle).execute();
    } catch (Exception ex) {
      log.error(
          "Failed to submit the batch of {} Library resource(s) to the FHIR server",
          libraryBundles.size(),
          ex);
      return new DeliveryResult(0, libraryBundles.size());
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

    return new DeliveryResult(succeeded, failed);
  }

  private record DeliveryResult(int succeeded, int failed) {}
}
