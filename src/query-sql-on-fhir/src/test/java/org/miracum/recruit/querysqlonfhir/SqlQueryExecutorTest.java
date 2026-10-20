package org.miracum.recruit.querysqlonfhir;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.contains;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.RETURNS_DEEP_STUBS;
import static org.mockito.Mockito.atLeastOnce;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.nio.charset.StandardCharsets;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.ArrayList;
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

  private final SqlQueryExecutor sut = new SqlQueryExecutor(jdbcTemplate, sqlOnFhirClient, false);

  /**
   * Rows the merged query's {@link ResultSet} should stream back - see {@link #stubJdbcTemplate}.
   */
  private final List<Map<String, Object>> mainQueryRows = new ArrayList<>();

  /**
   * The merged, all-Trino path is exercised through both a {@code LIMIT 0} column-probe query (see
   * {@code resolveResultColumns}) and the real merged query (see {@code evaluateAgainstTrino}) -
   * both go through the same {@code jdbcTemplate.query(String, ResultSetExtractor)} overload, so
   * this single stub distinguishes them by SQL content: a probe always wraps its criterion SQL with
   * {@code LIMIT 0}, the merged query never does. Probing both {@code SQL_AGE} and {@code
   * SQL_CHEMO} report {@code is_indeterminate}/{@code result_note} present; a test exercising a
   * criterion that omits either column overrides this stub for that criterion's specific probe SQL.
   * The merged query streams whatever {@link #mainQueryRows} the test has populated, through a fake
   * {@link ResultSet} - mirroring how the production code reads rows directly off the JDBC
   * ResultSet instead of collecting them into a list first.
   */
  @BeforeEach
  void stubJdbcTemplate() {
    when(jdbcTemplate.query(anyString(), any(ResultSetExtractor.class)))
        .thenAnswer(
            invocation -> {
              String sql = invocation.getArgument(0);
              if (sql.contains("LIMIT 0")) {
                return Set.of("is_indeterminate", "result_note");
              }
              ResultSetExtractor<?> extractor = invocation.getArgument(1);
              return extractor.extractData(fakeResultSet(mainQueryRows));
            });
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

  /**
   * A {@link ResultSet} double standing in for a real JDBC driver's, so the production
   * row-by-row-streaming code (see {@code SqlQueryExecutor#toPatientEligibilityResult}) can be
   * exercised without a real database. Relies on production code always calling {@code
   * rs.findColumn(name)} immediately before the corresponding {@code rs.getObject(index)} for that
   * same column (as {@link org.springframework.jdbc.support.JdbcUtils#getResultSetValue} does), so
   * the fake doesn't need to simulate real column indices - it just remembers the last column name
   * looked up and returns that row's value for it.
   */
  private static ResultSet fakeResultSet(List<Map<String, Object>> rows) throws SQLException {
    var rs = mock(ResultSet.class);
    var rowIndex = new int[] {-1};
    var lastColumn = new String[1];

    when(rs.next()).thenAnswer(invocation -> ++rowIndex[0] < rows.size());
    when(rs.findColumn(anyString()))
        .thenAnswer(
            invocation -> {
              lastColumn[0] = invocation.getArgument(0);
              return 1;
            });
    when(rs.getObject(anyInt())).thenAnswer(invocation -> rows.get(rowIndex[0]).get(lastColumn[0]));

    return rs;
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

  private static List<PatientEligibilityResult> collect(
      SqlQueryExecutor sut, List<EligibilityCriterion> criteria) {
    var collected = new ArrayList<PatientEligibilityResult>();
    sut.evaluateEligibility(criteria, collected::add);
    return collected;
  }

  @Test
  void evaluateEligibility_withEmptyCriteria_returnsEmptyAndTouchesNothing() {
    var result = collect(sut, List.of());

    assertThat(result).isEmpty();
    verifyNoInteractions(jdbcTemplate, sqlOnFhirClient);
  }

  @Test
  void evaluateEligibility_withAllTrinoDirectCriteria_runsOneMergedQuery() {
    var ageCriterion = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var chemoCriterion = new EligibilityCriterion(trinoLibrary(SQL_CHEMO), "No prior chemo", true);

    mainQueryRows.addAll(
        List.of(
            row("patient_id", "pat-1", "crit_0_is_met", true, "crit_1_is_met", true),
            row("patient_id", "pat-2", "crit_0_is_met", true, "crit_1_is_met", null)));

    var results = collect(sut, List.of(ageCriterion, chemoCriterion));

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

  /**
   * A criterion whose SQL joins against a one-to-many relation (e.g. an UNNESTed array) without a
   * DISTINCT/GROUP BY can return more than one row for the same patient_id - the merged query's
   * LEFT JOINs then fan that patient out into multiple result rows. Downstream, that would mean the
   * same ResearchSubject id gets written twice in one transaction bundle, which FHIR servers like
   * Blaze reject outright (failing the whole chunk) - so this is deduplicated defensively here,
   * keeping only the first row for a given patient_id.
   */
  @Test
  void evaluateEligibility_whenMergedQueryReturnsDuplicatePatientId_keepsOnlyFirstRow() {
    var ageCriterion = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);

    mainQueryRows.addAll(
        List.of(
            row("patient_id", "pat-1", "crit_0_is_met", true),
            row("patient_id", "pat-1", "crit_0_is_met", true),
            row("patient_id", "pat-2", "crit_0_is_met", true)));

    var results = collect(sut, List.of(ageCriterion));

    assertThat(results)
        .extracting(PatientEligibilityResult::patientId)
        .containsExactly("pat-1", "pat-2");
  }

  @Test
  void evaluateEligibility_generatesMergedQueryWithOneCteAndAppliesExcludeNegation() {
    var include = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var exclude = new EligibilityCriterion(trinoLibrary(SQL_CHEMO), "No prior chemo", true);

    var captor = ArgumentCaptor.forClass(String.class);
    collect(sut, List.of(include, exclude));
    verify(jdbcTemplate, atLeastOnce()).query(captor.capture(), any(ResultSetExtractor.class));

    var sql = mergedQuerySql(captor);
    assertThat(sql)
        .contains("WITH crit_0 AS (\nSELECT DISTINCT * FROM (\n" + SQL_AGE + "\n) AS crit_0_raw\n)")
        .contains("crit_1 AS (\nSELECT DISTINCT * FROM (\n" + SQL_CHEMO + "\n) AS crit_1_raw\n)")
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

    var captor = ArgumentCaptor.forClass(String.class);
    collect(sut, List.of(withBoth, withoutEither));
    verify(jdbcTemplate, atLeastOnce()).query(captor.capture(), any(ResultSetExtractor.class));

    var sql = mergedQuerySql(captor);
    assertThat(sql)
        .contains("crit_0.is_indeterminate AS crit_0_is_indeterminate")
        .contains("crit_0.result_note AS crit_0_result_note")
        .contains("CAST(NULL AS BOOLEAN) AS crit_1_is_indeterminate")
        .contains("CAST(NULL AS VARCHAR) AS crit_1_result_note")
        .doesNotContain("crit_1.is_indeterminate")
        .doesNotContain("crit_1.result_note");
  }

  /**
   * The merged query is one of possibly several {@code jdbcTemplate.query(String,
   * ResultSetExtractor)} calls a test triggers (one {@code LIMIT 0} probe per criterion, plus the
   * merged query itself) - this picks out the one call that isn't a probe.
   */
  private static String mergedQuerySql(ArgumentCaptor<String> captor) {
    return captor.getAllValues().stream()
        .filter(sql -> !sql.contains("LIMIT 0"))
        .findFirst()
        .orElseThrow();
  }

  @Test
  void evaluateEligibility_carriesIndeterminateAndNoteThroughWithoutAffectingOverallMet() {
    var ageCriterion = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);

    mainQueryRows.add(
        row(
            "patient_id",
            "pat-1",
            "crit_0_is_met",
            null,
            "crit_0_is_indeterminate",
            true,
            "crit_0_result_note",
            "conflicting lab results"));

    var results = collect(sut, List.of(ageCriterion));

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

    var results = collect(sut, List.of(trinoIncluded, delegatedExcluded));

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

    var results = collect(sut, List.of(criterion));

    assertThat(results).extracting(PatientEligibilityResult::patientId).containsExactly("pat-1");
    verifyNoInteractions(jdbcTemplate);
  }

  private static final String TOTAL_POPULATION_SQL = "SELECT COUNT(*) FROM fhir.qs.patient";

  @Test
  void computeFunnel_withEmptyCriteria_returnsEmptyAndTouchesNothing() {
    var result = sut.computeFunnel(List.of(), TOTAL_POPULATION_SQL);

    assertThat(result.totalPopulation()).isZero();
    assertThat(result.steps()).isEmpty();
    verifyNoInteractions(jdbcTemplate, sqlOnFhirClient);
  }

  @Test
  void computeFunnel_withAllTrinoDirectCriteria_returnsCascadingCounts() {
    var ageCriterion = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var chemoCriterion = new EligibilityCriterion(trinoLibrary(SQL_CHEMO), "No prior chemo", true);

    when(jdbcTemplate.queryForObject(eq(TOTAL_POPULATION_SQL), eq(Long.class))).thenReturn(12000L);
    when(jdbcTemplate.query(anyString(), any(ResultSetExtractor.class)))
        .thenAnswer(
            invocation -> {
              ResultSetExtractor<?> extractor = invocation.getArgument(1);
              return extractor.extractData(
                  fakeResultSet(List.of(row("crit_0_remaining", 8400L, "crit_1_remaining", 847L))));
            });

    var funnel = sut.computeFunnel(List.of(ageCriterion, chemoCriterion), TOTAL_POPULATION_SQL);

    assertThat(funnel.totalPopulation()).isEqualTo(12000L);
    assertThat(funnel.steps())
        .extracting(FunnelResult.Step::remainingCount)
        .containsExactly(8400L, 847L);
    assertThat(funnel.steps())
        .extracting(step -> step.criterion().displayText())
        .containsExactly("Age >= 18", "No prior chemo");
    verifyNoInteractions(sqlOnFhirClient);
  }

  @Test
  void computeFunnel_generatesFunnelQueryWithCascadingFilterConditions() {
    var include = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var exclude = new EligibilityCriterion(trinoLibrary(SQL_CHEMO), "No prior chemo", true);

    when(jdbcTemplate.queryForObject(eq(TOTAL_POPULATION_SQL), eq(Long.class))).thenReturn(0L);
    when(jdbcTemplate.query(anyString(), any(ResultSetExtractor.class)))
        .thenAnswer(
            invocation -> {
              ResultSetExtractor<?> extractor = invocation.getArgument(1);
              return extractor.extractData(
                  fakeResultSet(List.of(row("crit_0_remaining", 0L, "crit_1_remaining", 0L))));
            });

    var captor = ArgumentCaptor.forClass(String.class);
    sut.computeFunnel(List.of(include, exclude), TOTAL_POPULATION_SQL);
    verify(jdbcTemplate, atLeastOnce()).query(captor.capture(), any(ResultSetExtractor.class));

    var sql = captor.getValue();
    assertThat(sql)
        .contains("WITH crit_0 AS (\nSELECT DISTINCT * FROM (\n" + SQL_AGE + "\n) AS crit_0_raw\n)")
        .contains("crit_1 AS (\nSELECT DISTINCT * FROM (\n" + SQL_CHEMO + "\n) AS crit_1_raw\n)")
        .contains("LEFT JOIN crit_1 ON crit_1.patient_id = crit_0.patient_id")
        .contains(
            "COUNT(*) FILTER (WHERE ((crit_0.is_met)) IS DISTINCT FROM FALSE) AS crit_0_remaining")
        .contains(
            "COUNT(*) FILTER (WHERE ((crit_0.is_met) AND (NOT crit_1.is_met)) "
                + "IS DISTINCT FROM FALSE) AS crit_1_remaining");
  }

  /**
   * Same fixture as {@link
   * #evaluateEligibility_withMixOfTrinoAndSqlOnFhirCriteria_mergesInApplicationMemory} - pat-2 is
   * excluded once the chemo criterion is cumulatively applied (step 1), pat-1 and pat-3 (unknown)
   * remain.
   */
  @Test
  void computeFunnel_withMixOfTrinoAndSqlOnFhirCriteria_mergesInApplicationMemory() {
    var trinoIncluded = new EligibilityCriterion(trinoLibrary(SQL_AGE), "Age >= 18", false);
    var delegatedExcluded =
        new EligibilityCriterion(sqlOnFhirLibrary(SQL_CHEMO), "No prior chemo", true);

    when(jdbcTemplate.queryForObject(eq(TOTAL_POPULATION_SQL), eq(Long.class))).thenReturn(3L);
    when(jdbcTemplate.queryForList(contains("age")))
        .thenReturn(
            List.of(
                row("patient_id", "pat-1", "is_met", true),
                row("patient_id", "pat-2", "is_met", true),
                row("patient_id", "pat-3", "is_met", true)));

    stubSqlOnFhirResponse(
        parametersWithRows(
            List.of(
                row("patient_id", "pat-1", "is_met", false),
                row("patient_id", "pat-2", "is_met", true))));

    var funnel = sut.computeFunnel(List.of(trinoIncluded, delegatedExcluded), TOTAL_POPULATION_SQL);

    assertThat(funnel.totalPopulation()).isEqualTo(3L);
    assertThat(funnel.steps())
        .extracting(FunnelResult.Step::remainingCount)
        .containsExactly(3L, 2L);
  }
}
