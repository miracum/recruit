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
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.RelatedArtifact;
import org.hl7.fhir.r4.model.StringType;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.jdbc.core.JdbcTemplate;

class SqlQueryExecutorTest {

  private final JdbcTemplate jdbcTemplate = mock(JdbcTemplate.class);
  private final IGenericClient sqlOnFhirClient = mock(IGenericClient.class, RETURNS_DEEP_STUBS);

  private final SqlQueryExecutor sut = new SqlQueryExecutor(jdbcTemplate, sqlOnFhirClient);

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

  /** {@link Map#of} rejects null values, but a criterion's "met" column is legitimately null. */
  private static Map<String, Object> row(Object... keyValuePairs) {
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
    var ageCriterion =
        new EligibilityCriterion(
            trinoLibrary("SELECT patient_id, met FROM age"), "Age >= 18", false);
    var chemoCriterion =
        new EligibilityCriterion(
            trinoLibrary("SELECT patient_id, met FROM chemo"), "No prior chemo", true);

    when(jdbcTemplate.queryForList(anyString()))
        .thenReturn(
            List.of(
                row("patient_id", "pat-1", "crit_0_met", true, "crit_1_met", true),
                row("patient_id", "pat-2", "crit_0_met", true, "crit_1_met", null)));

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
    var include =
        new EligibilityCriterion(
            trinoLibrary("SELECT patient_id, met FROM age"), "Age >= 18", false);
    var exclude =
        new EligibilityCriterion(
            trinoLibrary("SELECT patient_id, met FROM chemo"), "No prior chemo", true);

    when(jdbcTemplate.queryForList(anyString())).thenReturn(List.of());

    var captor = ArgumentCaptor.forClass(String.class);
    sut.evaluateEligibility(List.of(include, exclude));
    org.mockito.Mockito.verify(jdbcTemplate).queryForList(captor.capture());

    var sql = captor.getValue();
    assertThat(sql)
        .contains("WITH crit_0 AS (\nSELECT patient_id, met FROM age\n)")
        .contains("crit_1 AS (\nSELECT patient_id, met FROM chemo\n)")
        .contains("LEFT JOIN crit_1 ON crit_1.patient_id = crit_0.patient_id")
        .contains("(crit_0.met) AS crit_0_met")
        .contains("(NOT crit_1.met) AS crit_1_met")
        .contains("WHERE ((crit_0.met) AND (NOT crit_1.met)) IS DISTINCT FROM FALSE");
  }

  @Test
  void evaluateEligibility_withMixOfTrinoAndSqlOnFhirCriteria_mergesInApplicationMemory() {
    var trinoIncluded =
        new EligibilityCriterion(
            trinoLibrary("SELECT patient_id, met FROM age"), "Age >= 18", false);
    var delegatedExcluded =
        new EligibilityCriterion(
            sqlOnFhirLibrary("SELECT patient_id, met FROM chemo"), "No prior chemo", true);

    when(jdbcTemplate.queryForList(contains("age")))
        .thenReturn(
            List.of(
                row("patient_id", "pat-1", "met", true),
                row("patient_id", "pat-2", "met", true),
                row("patient_id", "pat-3", "met", true)));

    stubSqlOnFhirResponse(
        parametersWithRows(
            List.of(
                // pat-1: no prior chemo -> raw false -> excluded=true negates to met=true
                row("patient_id", "pat-1", "met", false),
                // pat-2: had chemo -> raw true -> negates to met=false -> overall excluded
                row("patient_id", "pat-2", "met", true)
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
    var criterion =
        new EligibilityCriterion(
            sqlOnFhirLibrary("SELECT patient_id, met FROM age"), "Age >= 18", false);

    stubSqlOnFhirResponse(
        parametersWithRows(
            List.of(
                row("patient_id", "pat-1", "met", true),
                row("patient_id", "pat-2", "met", false))));

    var results = sut.evaluateEligibility(List.of(criterion));

    assertThat(results).extracting(PatientEligibilityResult::patientId).containsExactly("pat-1");
    verifyNoInteractions(jdbcTemplate);
  }
}
