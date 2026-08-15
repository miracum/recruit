package org.miracum.recruit.querysqlonfhir;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.contains;
import static org.mockito.Mockito.RETURNS_DEEP_STUBS;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.RelatedArtifact;
import org.hl7.fhir.r4.model.StringType;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.ResultSetExtractor;

class SqlQueryExecutorTest {

  private static final String SQL_AGE =
      "SELECT patient_id, is_met, CAST(NULL AS BOOLEAN) AS is_indeterminate, "
          + "CAST(NULL AS VARCHAR) AS result_note FROM age";
  private static final String SQL_CHEMO =
      "SELECT patient_id, is_met, CAST(NULL AS BOOLEAN) AS is_indeterminate, "
          + "CAST(NULL AS VARCHAR) AS result_note FROM chemo";

  private final JdbcTemplate jdbcTemplate = mock(JdbcTemplate.class);
  private final IGenericClient sqlOnFhirClient = mock(IGenericClient.class, RETURNS_DEEP_STUBS);

  private final SqlQueryExecutor sut = new SqlQueryExecutor(jdbcTemplate, sqlOnFhirClient);

  /**
   * Trino-direct criteria are probed with a {@code LIMIT 0} query to see which optional columns
   * they return, before the merged query referencing them is built. {@code SQL_AGE}/{@code
   * SQL_CHEMO} both select {@code is_indeterminate}/{@code result_note}, so this is what a real
   * probe against them would report; tests exercising a criterion that omits either column override
   * this stub.
   */
  @BeforeEach
  void stubColumnProbe() {
    when(jdbcTemplate.query(anyString(), any(ResultSetExtractor.class)))
        .thenReturn(Set.of("is_indeterminate", "result_note"));
  }

  private static Library trinoLibrary(String sql) {
    var library = new Library();
    library
        .addContent()
        .setContentType("application/sql;dialect=trino")
        .setData(sql.getBytes(StandardCharsets.UTF_8));
    return library;
  }

  private static Library sqlOnFhirLibrary(String sql) {
    var library = trinoLibrary(sql);
    library
        .addRelatedArtifact()
        .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
        .setLabel("patients")
        .setResource("ViewDefinition/patients");
    return library;
  }

  /** {@link Map#of} rejects null values, but a criterion's "is_met" column is legitimately null. */
  private static Map<String, Object> row(Object... keyValuePairs) {
    if (keyValuePairs.length % 2 != 0) {
      throw new IllegalArgumentException("keyValuePairs must have an even number of elements");
    }
    var row = new HashMap<String, Object>();
    for (var i = 0; i < keyValuePairs.length; i += 2) {
      row.put((String) keyValuePairs[i], keyValuePairs[i + 1]);
    }
    return row;
  }

  private void stubSqlOnFhirResponse(Parameters response) {
    when(sqlOnFhirClient
            .operation()
            .onServer()
            .named("$sqlquery-run")
            .withParameters(any(Parameters.class))
            .returnResourceType(Parameters.class)
            .execute())
        .thenReturn(response);
  }

  private static Parameters parametersWithRows(List<Map<String, Object>> rows) {
    var parameters = new Parameters();
    for (var row : rows) {
      var rowParam = new Parameters.ParametersParameterComponent().setName("row");
      row.forEach(
          (name, value) -> {
            if (value != null) {
              rowParam.addPart().setName(name).setValue(new StringType(value.toString()));
            }
          });
      parameters.addParameter(rowParam);
    }
    return parameters;
  }

  @Test
  void evaluateEligibility_withEmptyCriteria_returnsEmptyAndTouchesNothing() {
    var result = sut.evaluateEligibility(List.of());

    assertThat(result).isEmpty();
    verifyNoInteractions(jdbcTemplate, sqlOnFhirClient);
  }

  @Test
  void evaluateEligibility_withAllTrinoDirectCriteria_runsOneMergedQuery() {
    var ageCriterion = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var chemoCriterion = new EligibilityCriterion(trinoLibrary(SQL_CHEMO), "No prior chemo", true);

    when(jdbcTemplate.queryForList(anyString()))
        .thenReturn(
            List.of(
                row("patient_id", "pat-1", "crit_0_is_met", true, "crit_1_is_met", true),
                row("patient_id", "pat-2", "crit_0_is_met", true, "crit_1_is_met", null)));

    var results = sut.evaluateEligibility(List.of(ageCriterion, chemoCriterion));

    assertThat(results).hasSize(2);

    var pat1 =
        results.stream().filter(r -> r.patientId().equals("pat-1")).findFirst().orElseThrow();
    assertThat(pat1.overallMet()).isTrue();
    assertThat(pat1.criteria()).extracting(CriterionOutcome::met).containsExactly(true, true);

    var pat2 =
        results.stream().filter(r -> r.patientId().equals("pat-2")).findFirst().orElseThrow();
    assertThat(pat2.overallMet()).isNull();
    assertThat(pat2.criteria()).extracting(CriterionOutcome::met).containsExactly(true, null);

    verifyNoInteractions(sqlOnFhirClient);
  }

  @Test
  void evaluateEligibility_generatesMergedQueryWithOneCteAndAppliesExcludeNegation() {
    var include = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var exclude = new EligibilityCriterion(trinoLibrary(SQL_CHEMO), "No prior chemo", true);

    when(jdbcTemplate.queryForList(anyString())).thenReturn(List.of());

    var captor = ArgumentCaptor.forClass(String.class);
    sut.evaluateEligibility(List.of(include, exclude));
    org.mockito.Mockito.verify(jdbcTemplate).queryForList(captor.capture());

    var sql = captor.getValue();
    assertThat(sql)
        .contains("WITH crit_0 AS (\n" + SQL_AGE + "\n)")
        .contains("crit_1 AS (\n" + SQL_CHEMO + "\n)")
        .contains("LEFT JOIN crit_1 ON crit_1.patient_id = crit_0.patient_id")
        .contains("(crit_0.is_met) AS crit_0_is_met")
        .contains("crit_0.is_indeterminate AS crit_0_is_indeterminate")
        .contains("crit_0.result_note AS crit_0_result_note")
        .contains("(NOT crit_1.is_met) AS crit_1_is_met")
        .contains("crit_1.is_indeterminate AS crit_1_is_indeterminate")
        .contains("crit_1.result_note AS crit_1_result_note")
        .contains("WHERE ((crit_0.is_met) AND (NOT crit_1.is_met)) IS DISTINCT FROM FALSE");
  }

  @Test
  void
      evaluateEligibility_whenCriterionSqlOmitsOptionalColumns_substitutesNullLiteralsInMergedQuery() {
    var sqlWithoutOptionalColumns = "SELECT patient_id, is_met FROM age";
    var withBoth = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var withoutEither =
        new EligibilityCriterion(trinoLibrary(sqlWithoutOptionalColumns), "No prior chemo", true);

    when(jdbcTemplate.query(contains(sqlWithoutOptionalColumns), any(ResultSetExtractor.class)))
        .thenReturn(Set.of());
    when(jdbcTemplate.queryForList(anyString())).thenReturn(List.of());

    var captor = ArgumentCaptor.forClass(String.class);
    sut.evaluateEligibility(List.of(withBoth, withoutEither));
    org.mockito.Mockito.verify(jdbcTemplate).queryForList(captor.capture());

    var sql = captor.getValue();
    assertThat(sql)
        .contains("crit_0.is_indeterminate AS crit_0_is_indeterminate")
        .contains("crit_0.result_note AS crit_0_result_note")
        .contains("CAST(NULL AS BOOLEAN) AS crit_1_is_indeterminate")
        .contains("CAST(NULL AS VARCHAR) AS crit_1_result_note")
        .doesNotContain("crit_1.is_indeterminate")
        .doesNotContain("crit_1.result_note");
  }

  @Test
  void evaluateEligibility_carriesIndeterminateAndNoteThroughWithoutAffectingOverallMet() {
    var ageCriterion = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);

    when(jdbcTemplate.queryForList(anyString()))
        .thenReturn(
            List.of(
                row(
                    "patient_id",
                    "pat-1",
                    "crit_0_is_met",
                    null,
                    "crit_0_is_indeterminate",
                    true,
                    "crit_0_result_note",
                    "conflicting lab results")));

    var results = sut.evaluateEligibility(List.of(ageCriterion));

    assertThat(results).hasSize(1);
    var pat1 = results.get(0);
    assertThat(pat1.overallMet()).isNull();
    assertThat(pat1.criteria()).hasSize(1);
    assertThat(pat1.criteria().get(0).met()).isNull();
    assertThat(pat1.criteria().get(0).indeterminate()).isTrue();
    assertThat(pat1.criteria().get(0).note()).isEqualTo("conflicting lab results");
  }

  @Test
  void evaluateEligibility_withMixOfTrinoAndSqlOnFhirCriteria_mergesInApplicationMemory() {
    var trinoIncluded = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var delegatedExcluded =
        new EligibilityCriterion(sqlOnFhirLibrary(SQL_CHEMO), "No prior chemo", true);

    when(jdbcTemplate.queryForList(contains("age")))
        .thenReturn(
            List.of(
                row("patient_id", "pat-1", "is_met", true),
                row("patient_id", "pat-2", "is_met", true),
                row("patient_id", "pat-3", "is_met", true)));

    stubSqlOnFhirResponse(
        parametersWithRows(
            List.of(
                // pat-1: no prior chemo -> raw false -> excluded=true negates to met=true
                row("patient_id", "pat-1", "is_met", false),
                // pat-2: had chemo -> raw true -> negates to met=false -> overall excluded
                row("patient_id", "pat-2", "is_met", true)
                // pat-3 missing entirely from the delegated result -> unknown
                )));

    var results = sut.evaluateEligibility(List.of(trinoIncluded, delegatedExcluded));

    assertThat(results)
        .extracting(PatientEligibilityResult::patientId)
        .containsExactlyInAnyOrder("pat-1", "pat-3");

    var pat1 =
        results.stream().filter(r -> r.patientId().equals("pat-1")).findFirst().orElseThrow();
    assertThat(pat1.overallMet()).isTrue();

    var pat3 =
        results.stream().filter(r -> r.patientId().equals("pat-3")).findFirst().orElseThrow();
    assertThat(pat3.overallMet()).isNull();
  }

  @Test
  void evaluateEligibility_withSingleSqlOnFhirDelegatedCriterion_stillFiltersDefiniteNonMatches() {
    var criterion = new EligibilityCriterion(sqlOnFhirLibrary(SQL_AGE), "Age >= 18", false);

    stubSqlOnFhirResponse(
        parametersWithRows(
            List.of(
                row("patient_id", "pat-1", "is_met", true),
                row("patient_id", "pat-2", "is_met", false))));

    var results = sut.evaluateEligibility(List.of(criterion));

    assertThat(results).extracting(PatientEligibilityResult::patientId).containsExactly("pat-1");
    verifyNoInteractions(jdbcTemplate);
  }
}
