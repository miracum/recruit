package org.miracum.recruit.supersetlibrarysync.annotation;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class SqlAnnotationParserTest {

  private final SqlAnnotationParser sut = new SqlAnnotationParser();

  /** The exact example block from the IG's "SQL Annotations" section. */
  private static final String IG_EXAMPLE_SQL =
      """
      /*
      @name: PatientBloodPressure
      @title: Patient Blood Pressure Report
      @version: 1.0.0
      @status: active
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
  void parse_withIgExample_extractsBlockCommentAnnotations() {
    var result = sut.parse(IG_EXAMPLE_SQL);

    assertThat(result.name()).isEqualTo("PatientBloodPressure");
    assertThat(result.title()).isEqualTo("Patient Blood Pressure Report");
    assertThat(result.version()).isEqualTo("1.0.0");
    assertThat(result.status()).isEqualTo("active");
  }

  @Test
  void parse_withIgExample_extractsRepeatableLineCommentAnnotations() {
    var result = sut.parse(IG_EXAMPLE_SQL);

    assertThat(result.parameters())
        .containsExactly(
            new SqlParameter("patient_id", "string", "Patient identifier"),
            new SqlParameter("from_date", "date", "Start date"));

    assertThat(result.relatedDependencies())
        .containsExactly(
            new RelatedDependency("https://example.org/ViewDefinition/patient_view", "patient"),
            new RelatedDependency("https://example.org/ViewDefinition/bp_view", "bp"));
  }

  @Test
  void parse_withoutAnyAnnotations_returnsEmptyResult() {
    var result = sut.parse("SELECT 1");

    assertThat(result.isEmpty()).isTrue();
  }

  @Test
  void parse_withRepeatedAuthorAnnotations_accumulatesAllOfThem() {
    var sql =
        """
        -- @author: Alice
        -- @author: Bob
        SELECT 1
        """;

    var result = sut.parse(sql);

    assertThat(result.authors()).containsExactly("Alice", "Bob");
  }

  @Test
  void parse_withDuplicateSingleValuedAnnotation_keepsFirstOccurrence() {
    var sql =
        """
        -- @status: active
        -- @status: retired
        SELECT 1
        """;

    var result = sut.parse(sql);

    assertThat(result.status()).isEqualTo("active");
  }

  @Test
  void parse_withUnrecognizedAnnotation_ignoresItWithoutFailing() {
    var sql =
        """
        -- @jurisdiction: US
        -- @title: Still Parsed
        SELECT 1
        """;

    var result = sut.parse(sql);

    assertThat(result.title()).isEqualTo("Still Parsed");
  }

  @Test
  void parse_withParamMissingType_isDroppedButDoesNotFailOtherAnnotations() {
    var sql =
        """
        -- @param: onlyname
        -- @title: Kept
        SELECT 1
        """;

    var result = sut.parse(sql);

    assertThat(result.parameters()).isEmpty();
    assertThat(result.title()).isEqualTo("Kept");
  }

  @Test
  void parse_withParamOfUnsupportedType_isDropped() {
    var sql = "-- @param: patient_id uuid\nSELECT 1";

    var result = sut.parse(sql);

    assertThat(result.parameters()).isEmpty();
  }

  @Test
  void parse_withParamWithoutDescription_parsesNameAndTypeOnly() {
    var sql = "-- @param: patient_id string\nSELECT 1";

    var result = sut.parse(sql);

    assertThat(result.parameters()).containsExactly(new SqlParameter("patient_id", "string", null));
  }

  @Test
  void parse_withRelatedDependencyWithoutLabel_parsesUrlOnly() {
    var sql = "-- @relatedDependency: https://example.org/ViewDefinition/patient_view\nSELECT 1";

    var result = sut.parse(sql);

    assertThat(result.relatedDependencies())
        .containsExactly(
            new RelatedDependency("https://example.org/ViewDefinition/patient_view", null));
  }

  @Test
  void parse_withRelatedDependencyLabelNotValidSqlIdentifier_dropsOnlyTheLabel() {
    var sql =
        "-- @relatedDependency: https://example.org/ViewDefinition/patient_view as not-valid\nSELECT 1";

    var result = sut.parse(sql);

    assertThat(result.relatedDependencies())
        .containsExactly(
            new RelatedDependency("https://example.org/ViewDefinition/patient_view", null));
  }

  @Test
  void parse_withLineCommentInsideBlockComment_isNotDoubleCounted() {
    var sql =
        """
        /*
        @title: Only Once
        -- this looks like a line comment but is inside the block comment
        */
        SELECT 1
        """;

    var result = sut.parse(sql);

    assertThat(result.title()).isEqualTo("Only Once");
  }
}
