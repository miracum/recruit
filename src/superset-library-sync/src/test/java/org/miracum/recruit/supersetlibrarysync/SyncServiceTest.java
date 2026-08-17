package org.miracum.recruit.supersetlibrarysync;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.RETURNS_DEEP_STUBS;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import io.github.dizuker.tofhir.IdUtils;
import io.github.miracum.recruit.Recruit;
import java.util.List;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Identifier;
import org.junit.jupiter.api.Test;
import org.miracum.recruit.supersetlibrarysync.annotation.SqlAnnotationParser;
import org.miracum.recruit.supersetlibrarysync.library.SqlLibraryBuilder;
import org.miracum.recruit.supersetlibrarysync.superset.SavedQuery;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetProperties;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetSavedQueryRepository;
import org.mockito.ArgumentCaptor;

class SyncServiceTest {

  private static final FhirContext fhirContext = FhirContext.forR4();

  private final SupersetSavedQueryRepository savedQueryRepository =
      mock(SupersetSavedQueryRepository.class);
  private final IGenericClient fhirClient = mock(IGenericClient.class, RETURNS_DEEP_STUBS);

  private final SupersetProperties properties = new SupersetProperties("trino");

  private final SyncService sut = newSyncService(new SyncProperties(false));

  private SyncService newSyncService(SyncProperties syncProperties) {
    return new SyncService(
        savedQueryRepository,
        syncProperties,
        new SqlAnnotationParser(),
        new SqlLibraryBuilder(properties),
        fhirClient,
        fhirContext);
  }

  private static SavedQuery savedQuery(long id, String label, String sql) {
    return new SavedQuery(id, label, null, "public", sql, null, null, null, null);
  }

  @Test
  void sync_withSavedQueryHavingNoAnnotations_skipsItWithoutSubmitting() {
    when(savedQueryRepository.findAll()).thenReturn(List.of(savedQuery(1, "plain", "SELECT 1")));

    var result = sut.sync();

    assertThat(result.savedQueriesConsidered()).isEqualTo(1);
    assertThat(result.synced()).isZero();
    assertThat(result.failed()).isZero();
    verifyNoInteractions(fhirClient);
  }

  @Test
  void sync_withSavedQueryMissingNameAnnotation_skipsItWithoutSubmitting() {
    when(savedQueryRepository.findAll())
        .thenReturn(List.of(savedQuery(1, "no name", "-- @title: No Name Here\nSELECT 1")));

    var result = sut.sync();

    assertThat(result.savedQueriesConsidered()).isEqualTo(1);
    assertThat(result.synced()).isZero();
    assertThat(result.failed()).isZero();
    verifyNoInteractions(fhirClient);
  }

  @Test
  void sync_withOnePreparationFailure_isolatesItFromTheRest() {
    when(savedQueryRepository.findAll())
        .thenReturn(
            List.of(
                savedQuery(
                    1, "broken", "-- @name: Broken\n-- @status: not-a-real-status\nSELECT 1"),
                savedQuery(2, "ok", "-- @name: Ok\nSELECT 1")));

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
  void sync_submitsBatchBundleWithIdBasedPutPerLibrary() {
    when(savedQueryRepository.findAll())
        .thenReturn(List.of(savedQuery(1, "a", "-- @name: TestLibrary\nSELECT 1")));

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

    var expectedIdentifier =
        new Identifier()
            .setSystem(Recruit.NamingSystems.SupersetSyncedEligibilityLibraryId.uri())
            .setValue("testlibrary");
    var expectedId = IdUtils.fromIdentifier(expectedIdentifier);

    var entry = submittedBundle.getEntry().getFirst();
    assertThat(entry.getRequest().getMethod()).isEqualTo(Bundle.HTTPVerb.PUT);
    assertThat(entry.getRequest().getUrl()).isEqualTo("Library/" + expectedId);
  }

  @Test
  void sync_withDryRunEnabled_preparesLibrariesButDoesNotSubmitThem() {
    var dryRunSut = newSyncService(new SyncProperties(true));
    when(savedQueryRepository.findAll())
        .thenReturn(List.of(savedQuery(1, "a", "-- @name: TestLibrary\nSELECT 1")));

    var result = dryRunSut.sync();

    assertThat(result.savedQueriesConsidered()).isEqualTo(1);
    assertThat(result.synced()).isEqualTo(1);
    assertThat(result.failed()).isZero();
    verifyNoInteractions(fhirClient);
  }
}
