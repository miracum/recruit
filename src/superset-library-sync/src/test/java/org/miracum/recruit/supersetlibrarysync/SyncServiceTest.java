package org.miracum.recruit.supersetlibrarysync;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.RETURNS_DEEP_STUBS;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.io.IOException;
import java.net.URI;
import java.util.List;
import org.hl7.fhir.r4.model.Bundle;
import org.junit.jupiter.api.Test;
import org.miracum.recruit.supersetlibrarysync.annotation.SqlAnnotationParser;
import org.miracum.recruit.supersetlibrarysync.library.SqlLibraryBuilder;
import org.miracum.recruit.supersetlibrarysync.superset.SavedQuery;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetClient;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetProperties;
import org.mockito.ArgumentCaptor;

class SyncServiceTest {

  private final SupersetClient supersetClient = mock(SupersetClient.class);
  private final IGenericClient fhirClient = mock(IGenericClient.class, RETURNS_DEEP_STUBS);

  private final SupersetProperties properties =
      new SupersetProperties(
          URI.create("https://superset.example.org"), "admin", "admin", null, "trino", 100, 60);

  private final SyncService sut =
      new SyncService(
          supersetClient,
          properties,
          new SqlAnnotationParser(),
          new SqlLibraryBuilder(properties),
          fhirClient);

  private static SavedQuery savedQuery(long id, String label, String sql) {
    return new SavedQuery(id, label, null, "public", sql, List.of());
  }

  @Test
  void sync_withSavedQueryHavingNoAnnotations_skipsItWithoutSubmitting() throws Exception {
    when(supersetClient.listSavedQueries()).thenReturn(List.of(savedQuery(1, "plain", "SELECT 1")));
    when(supersetClient.getSavedQuery(1)).thenReturn(savedQuery(1, "plain", "SELECT 1"));

    var result = sut.sync();

    assertThat(result.savedQueriesConsidered()).isEqualTo(1);
    assertThat(result.synced()).isZero();
    assertThat(result.failed()).isZero();
  }

  @Test
  void sync_withOneQueryFailingToFetch_isolatesTheFailureFromTheRest() throws Exception {
    when(supersetClient.listSavedQueries())
        .thenReturn(
            List.of(savedQuery(1, "broken", ""), savedQuery(2, "ok", "-- @title: Ok\nSELECT 1")));
    when(supersetClient.getSavedQuery(1)).thenThrow(new IOException("boom"));
    when(supersetClient.getSavedQuery(2))
        .thenReturn(savedQuery(2, "ok", "-- @title: Ok\nSELECT 1"));

    var responseBundle = new Bundle();
    responseBundle.addEntry().getResponse().setStatus("200 OK");
    when(fhirClient.transaction().withBundle(any(Bundle.class)).execute())
        .thenReturn(responseBundle);

    var result = sut.sync();

    assertThat(result.savedQueriesConsidered()).isEqualTo(2);
    assertThat(result.synced()).isEqualTo(1);
    assertThat(result.failed()).isEqualTo(1);
  }

  @Test
  void sync_submitsBatchBundleWithConditionalPutPerLibrary() throws Exception {
    when(supersetClient.listSavedQueries())
        .thenReturn(List.of(savedQuery(1, "a", "-- @title: A\nSELECT 1")));
    when(supersetClient.getSavedQuery(1)).thenReturn(savedQuery(1, "a", "-- @title: A\nSELECT 1"));

    var responseBundle = new Bundle();
    responseBundle.addEntry().getResponse().setStatus("201 Created");

    var bundleCaptor = ArgumentCaptor.forClass(Bundle.class);
    when(fhirClient.transaction().withBundle(bundleCaptor.capture()).execute())
        .thenReturn(responseBundle);

    var result = sut.sync();

    assertThat(result.synced()).isEqualTo(1);

    var submittedBundle = bundleCaptor.getValue();
    assertThat(submittedBundle.getType()).isEqualTo(Bundle.BundleType.BATCH);
    assertThat(submittedBundle.getEntry()).hasSize(1);

    var entry = submittedBundle.getEntry().getFirst();
    assertThat(entry.getRequest().getMethod()).isEqualTo(Bundle.HTTPVerb.PUT);
    assertThat(entry.getRequest().getUrl())
        .isEqualTo("Library?identifier=https://superset.example.org/api/v1/saved_query|1");
  }

  @Test
  void sync_withTagFilterConfigured_onlyConsidersMatchingSavedQueries() throws Exception {
    var propertiesWithTag =
        new SupersetProperties(
            URI.create("https://superset.example.org"),
            "admin",
            "admin",
            "wanted",
            "trino",
            100,
            60);
    var sutWithTagFilter =
        new SyncService(
            supersetClient,
            propertiesWithTag,
            new SqlAnnotationParser(),
            new SqlLibraryBuilder(propertiesWithTag),
            fhirClient);

    var tagged =
        new SavedQuery(
            1,
            "tagged",
            null,
            "public",
            "-- @title: Tagged\nSELECT 1",
            List.of(new SavedQuery.Tag(1, "wanted")));
    var untagged = new SavedQuery(2, "untagged", null, "public", "SELECT 1", List.of());
    when(supersetClient.listSavedQueries()).thenReturn(List.of(tagged, untagged));
    when(supersetClient.getSavedQuery(1)).thenReturn(tagged);
    // Mockito's deep-stub answer can't resolve IGenericClient#transaction()'s generic return type
    // without an explicit `when`, so the chain must be stubbed even though this test doesn't
    // care about the submission outcome - only that filtering happened before it.
    when(fhirClient.transaction().withBundle(any(Bundle.class)).execute()).thenReturn(new Bundle());

    var result = sutWithTagFilter.sync();

    assertThat(result.savedQueriesConsidered()).isEqualTo(1);
  }
}
