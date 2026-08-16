package org.miracum.recruit.supersetlibrarysync.library;

import ca.uhn.fhir.context.FhirContext;
import java.net.URI;
import java.util.List;
import org.approvaltests.Approvals;
import org.approvaltests.core.Options;
import org.junit.jupiter.api.Test;
import org.miracum.recruit.supersetlibrarysync.annotation.SqlAnnotationParser;
import org.miracum.recruit.supersetlibrarysync.superset.SavedQuery;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetProperties;

class SqlLibraryBuilderTest {

  private static final FhirContext fhirContext = FhirContext.forR4();

  private final SqlAnnotationParser annotationParser = new SqlAnnotationParser();

  private final SqlLibraryBuilder sut =
      new SqlLibraryBuilder(
          new SupersetProperties(
              URI.create("https://superset.example.org"),
              "admin",
              "admin",
              "fhir-library",
              "trino",
              100,
              60));

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
    var savedQuery =
        new SavedQuery(
            42, "Patient Blood Pressure", "A saved query", "public", ANNOTATED_SQL, List.of());
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
            List.of());
    var annotations = annotationParser.parse(savedQuery.sql());

    var library = sut.build(savedQuery, annotations);

    var json = fhirContext.newJsonParser().setPrettyPrint(true).encodeResourceToString(library);
    Approvals.verify(json, new Options().forFile().withExtension(".fhir.json"));
  }
}
