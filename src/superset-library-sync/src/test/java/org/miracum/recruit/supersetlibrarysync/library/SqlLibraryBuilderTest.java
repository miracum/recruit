package org.miracum.recruit.supersetlibrarysync.library;

import static org.assertj.core.api.Assertions.assertThat;

import ca.uhn.fhir.context.FhirContext;
import java.time.Instant;
import org.approvaltests.Approvals;
import org.approvaltests.core.Options;
import org.junit.jupiter.api.Test;
import org.miracum.recruit.supersetlibrarysync.annotation.SqlAnnotationParser;
import org.miracum.recruit.supersetlibrarysync.superset.SavedQuery;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetProperties;

class SqlLibraryBuilderTest {

  private static final FhirContext fhirContext = FhirContext.forR4();

  private final SqlAnnotationParser annotationParser = new SqlAnnotationParser();

  private final SqlLibraryBuilder sut = new SqlLibraryBuilder(new SupersetProperties("trino"));

  private static final String ANNOTATED_SQL =
      """
      /*
      @name: PatientBloodPressure
      @title: Patient Blood Pressure Report
      @description: Adults with an elevated most recent blood pressure reading.
      @version: 1.0.0
      @status: active
      @author: Clinical Informatics Team
      @publisher: Regional Medical Center
      */

      -- @param: patient_id string Patient identifier
      -- @param: from_date date Start date
      -- @relatedDependency: https://example.org/ViewDefinition/patient_view as patient
      -- @relatedDependency: https://example.org/ViewDefinition/bp_view as bp

      SELECT patient.id, bp.systolic
      FROM patient JOIN bp ON patient.id = bp.patient_id
      WHERE patient.id = :patient_id AND bp.effective_date >= :from_date
      """;

  @Test
  void build_withFullyAnnotatedSavedQuery_producesExpectedLibrary() {
    // the saved query has a known creator too, but the @author annotation takes precedence over
    // it, so this creator must not show up in the approved output.
    var savedQuery =
        new SavedQuery(
            42,
            "Patient Blood Pressure",
            "A saved query",
            "public",
            ANNOTATED_SQL,
            "asmith",
            "Alex",
            "Smith",
            Instant.parse("2024-03-05T10:15:30Z"));
    var annotations = annotationParser.parse(savedQuery.sql());

    var library = sut.build(savedQuery, annotations);

    var json = fhirContext.newJsonParser().setPrettyPrint(true).encodeResourceToString(library);
    Approvals.verify(json, new Options().forFile().withExtension(".fhir.json"));
  }

  @Test
  void build_withoutOptionalAnnotations_fallsBackToSavedQueryMetadata() {
    var savedQuery =
        new SavedQuery(
            7,
            "Ad Hoc Report",
            "Some description",
            "public",
            "-- @name: Minimal\nSELECT 1",
            "jdoe",
            "Jane",
            "Doe",
            Instant.parse("2024-01-15T08:00:00Z"));
    var annotations = annotationParser.parse(savedQuery.sql());

    var library = sut.build(savedQuery, annotations);

    var json = fhirContext.newJsonParser().setPrettyPrint(true).encodeResourceToString(library);
    Approvals.verify(json, new Options().forFile().withExtension(".fhir.json"));
  }

  @Test
  void build_withNameAnnotationContainingSeparators_slugifiesIt() {
    var savedQuery =
        new SavedQuery(
            9,
            "Patient Blood Pressure",
            null,
            "public",
            "-- @name: Patient Blood Pressure Report\nSELECT 1",
            null,
            null,
            null,
            null);
    var annotations = annotationParser.parse(savedQuery.sql());

    var library = sut.build(savedQuery, annotations);

    assertThat(library.getIdentifierFirstRep().getValue())
        .isEqualTo("patient-blood-pressure-report");
  }

  @Test
  void build_withNonAnnotationComment_keepsItInStoredContent() {
    var savedQuery =
        new SavedQuery(
            13,
            "Ad Hoc Report",
            "Some description",
            "public",
            """
            -- @name: Minimal
            -- this query intentionally only returns a constant
            SELECT 1
            """,
            null,
            null,
            null,
            null);
    var annotations = annotationParser.parse(savedQuery.sql());

    var library = sut.build(savedQuery, annotations);

    assertThat(library.getAuthor()).isEmpty();
    assertThat(library.hasDate()).isFalse();
    var json = fhirContext.newJsonParser().setPrettyPrint(true).encodeResourceToString(library);
    Approvals.verify(json, new Options().forFile().withExtension(".fhir.json"));
  }
}
