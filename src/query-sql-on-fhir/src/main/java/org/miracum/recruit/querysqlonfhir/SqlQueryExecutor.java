package org.miracum.recruit.querysqlonfhir;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.nio.charset.StandardCharsets;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.function.Consumer;
import org.hl7.fhir.r4.model.Attachment;
import org.hl7.fhir.r4.model.CodeType;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.PrimitiveType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.ResultSetExtractor;
import org.springframework.jdbc.support.JdbcUtils;
import org.springframework.stereotype.Component;

/**
 * Evaluates a study's eligibility criteria (one per {@code Group.characteristic}, each backed by a
 * <a
 * href="https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/StructureDefinition-SQLQuery.html">SQLQuery</a>
 * Library) and merges the per-criterion results into one outcome per patient using three-valued
 * (Kleene) logic: a definite "not met" on any criterion dominates, and the overall result is
 * "unknown" only when nothing is definitely not met but at least one criterion's data is missing.
 *
 * <p>Every criterion Library's SQL must return {@code patient_id} and {@code is_met} (nullable
 * boolean), covering the full patient population so that missing data reliably produces a {@code
 * null} row rather than a missing one. It may optionally also return {@code is_indeterminate}
 * (boolean, only meaningful when {@code is_met} is {@code null}) and {@code result_note} (nullable
 * string, a free-text explanation carried through to {@code Observation.value.text} - most useful
 * alongside indeterminate, but not limited to it); a criterion whose SQL omits either column is
 * treated as if it had selected {@code CAST(NULL AS BOOLEAN)} / {@code CAST(NULL AS VARCHAR)} for
 * it.
 *
 * <p>When every criterion's Library has {@code application/sql;dialect=trino} content and no {@code
 * relatedArtifact}, the merge is expressed as a single generated SQL query (one CTE per criterion)
 * run directly against Trino - this is the scalable path, since it never pulls a full population's
 * worth of rows over JDBC into this module's heap. If any criterion instead needs delegating to the
 * sql-on-fhir server's {@code $sqlquery-run} operation (because it depends on a ViewDefinition, or
 * uses a different dialect), the merge falls back to resolving each criterion's full-population
 * result independently and merging them in application memory - this does not scale as well and
 * should be avoided for large patient populations.
 */
@Component
public class SqlQueryExecutor {

  private static final Logger log = LoggerFactory.getLogger(SqlQueryExecutor.class);

  private static final String TRINO_SQL_CONTENT_TYPE = "application/sql;dialect=trino";
  private static final String PATIENT_ID_COLUMN = "patient_id";
  private static final String IS_MET_COLUMN = "is_met";
  private static final String IS_INDETERMINATE_COLUMN = "is_indeterminate";
  private static final String RESULT_NOTE_COLUMN = "result_note";
  private static final String ROW_PARAMETER = "row";

  private final JdbcTemplate jdbcTemplate;
  private final IGenericClient sqlOnFhirClient;
  private final boolean requireAllCriteriaMet;

  public SqlQueryExecutor(
      JdbcTemplate jdbcTemplate,
      @Qualifier("sqlOnFhirClient") IGenericClient sqlOnFhirClient,
      @Value("${query-sql-on-fhir.require-all-criteria-met:false}") boolean requireAllCriteriaMet) {
    this.jdbcTemplate = jdbcTemplate;
    this.sqlOnFhirClient = sqlOnFhirClient;
    this.requireAllCriteriaMet = requireAllCriteriaMet;
  }

  /**
   * Evaluates every criterion for every patient and invokes {@code onResult} once per patient who
   * is either a candidate (all criteria met) or undecidable (nothing definitely not met, but at
   * least one unknown). Patients for whom at least one criterion is definitely not met are never
   * passed to {@code onResult}.
   *
   * <p>On the all-Trino path, results are streamed row-by-row straight from the JDBC {@link
   * ResultSet} - at most one patient's worth of data is ever held in memory here, regardless of
   * population size. The delegated fallback path cannot offer that guarantee (see {@link
   * #evaluateWithFallbackMerge}) and instead resolves its full result in memory before invoking
   * {@code onResult} for each entry.
   */
  public void evaluateEligibility(
      List<EligibilityCriterion> criteria, Consumer<PatientEligibilityResult> onResult) {
    if (criteria.isEmpty()) {
      return;
    }

    if (criteria.stream().allMatch(this::isTrinoDirect)) {
      evaluateAgainstTrino(criteria, onResult);
      return;
    }

    log.warn(
        "Study has a mix of Trino-direct and sql-on-fhir-delegated criteria; falling back to "
            + "resolving each criterion independently and merging in application memory.");
    evaluateWithFallbackMerge(criteria, onResult);
  }

  private boolean isTrinoDirect(EligibilityCriterion criterion) {
    return !criterion.library().hasRelatedArtifact()
        && findTrinoDialectContent(criterion.library()).isPresent();
  }

  private Optional<Attachment> findTrinoDialectContent(Library library) {
    return library.getContent().stream()
        .filter(a -> isTrinoDialect(a.getContentType()))
        .findFirst();
  }

  private boolean isTrinoDialect(String contentType) {
    if (contentType == null) {
      return false;
    }
    var normalized = contentType.replaceAll("\\s+", "").toLowerCase(Locale.ROOT);
    return TRINO_SQL_CONTENT_TYPE.equals(normalized);
  }

  private String trinoSql(EligibilityCriterion criterion) {
    return new String(
        findTrinoDialectContent(criterion.library()).orElseThrow().getData(),
        StandardCharsets.UTF_8);
  }

  // ---- all-Trino path: one generated, merged query ----

  private void evaluateAgainstTrino(
      List<EligibilityCriterion> criteria, Consumer<PatientEligibilityResult> onResult) {
    var criterionColumns = criteria.stream().map(c -> resolveResultColumns(trinoSql(c))).toList();

    var sql = buildMergedQuery(criteria, criterionColumns);
    log.info(
        "Running merged eligibility query for {} criteria against Trino:\n{}",
        criteria.size(),
        sql);

    // A criterion's SQL is expected to return at most one row per patient_id - the merged query's
    // LEFT JOINs assume it, since a criterion CTE with more than one row for the same patient
    // (e.g. one whose WHERE joins against an UNNESTed array without a DISTINCT/GROUP BY) fans that
    // patient out into multiple rows here, which would otherwise reach the caller as duplicate
    // PatientEligibilityResults - and, downstream, as the same ResearchSubject id being written
    // twice in one transaction bundle, failing the whole chunk. Deduplicated defensively, since
    // that's a data problem in one criterion's SQL, not a reason to drop every other patient in the
    // same chunk.
    var seenPatientIds = new HashSet<String>();
    jdbcTemplate.query(
        sql,
        (ResultSetExtractor<Void>)
            rs -> {
              while (rs.next()) {
                var result = toPatientEligibilityResult(rs, criteria);
                if (seenPatientIds.add(result.patientId())) {
                  onResult.accept(result);
                } else {
                  log.warn(
                      "Merged eligibility query returned patient_id={} more than once; keeping "
                          + "the first row and dropping the rest. This usually means one "
                          + "criterion's SQL joins against a one-to-many relation (e.g. an "
                          + "UNNESTed array) without a DISTINCT/GROUP BY, so it returns more than "
                          + "one row for this patient.",
                      result.patientId());
                }
              }
              return null;
            });
  }

  /**
   * The lowercased column labels a criterion's SQL actually returns, found by probing it with a
   * {@code LIMIT 0} wrapper - cheap since Trino doesn't need to scan any data to plan a query with
   * no rows, and independent of the SQL's own result count. Used to tell whether the optional
   * {@code is_indeterminate}/{@code result_note} columns are present before they're referenced by
   * name in the generated merged query, since referencing a column a criterion's SQL doesn't
   * actually select would otherwise fail the whole merged query at the database.
   */
  private Set<String> resolveResultColumns(String sql) {
    var probeSql = "SELECT * FROM (\n" + sql + "\n) AS probe LIMIT 0";
    return jdbcTemplate.query(
        probeSql,
        rs -> {
          var metaData = rs.getMetaData();
          var columns = new LinkedHashSet<String>();
          for (var i = 1; i <= metaData.getColumnCount(); i++) {
            columns.add(metaData.getColumnLabel(i).toLowerCase(Locale.ROOT));
          }
          return columns;
        });
  }

  /**
   * Wraps each criterion's already-standalone SQL as a {@code SELECT DISTINCT}-deduplicated CTE,
   * joins them on {@code patient_id} (anchored on the first criterion, which - per the two-column
   * contract - already covers the full patient population), and filters out definite non-matches
   * via native Kleene {@code AND} pushed down into the {@code WHERE} clause. See {@code
   * docs/trino/eligibility-criteria-design.md} for the full rationale.
   *
   * <p>The {@code SELECT DISTINCT} wrapping matters because the {@code LEFT JOIN}s below assume
   * each criterion contributes at most one row per {@code patient_id} - a criterion whose SQL joins
   * against a one-to-many relation (e.g. an {@code UNNEST}ed array) without its own {@code
   * DISTINCT}/{@code GROUP BY} would otherwise fan that patient out into duplicate rows here. This
   * only collapses rows that come out fully identical, though; a criterion returning genuinely
   * conflicting rows for the same patient (e.g. {@code is_met} both {@code true} and {@code false})
   * is a data problem in that criterion's SQL this can't safely resolve - {@link
   * #evaluateAgainstTrino} still guards against that case defensively.
   */
  private String buildMergedQuery(
      List<EligibilityCriterion> criteria, List<Set<String>> criterionColumns) {
    var ctes = new StringBuilder();
    var selectColumns = new StringBuilder();
    var joins = new StringBuilder();
    var terms = new ArrayList<String>();

    for (var i = 0; i < criteria.size(); i++) {
      var criterion = criteria.get(i);
      var alias = "crit_" + i;
      var columns = criterionColumns.get(i);

      ctes.append(i == 0 ? "WITH " : ",\n     ")
          .append(alias)
          .append(" AS (\n")
          .append("SELECT DISTINCT * FROM (\n")
          .append(trinoSql(criterion))
          .append("\n) AS ")
          .append(alias)
          .append("_raw\n)");

      // exclude negation is applied once, here, generically - criterion SQL always computes the
      // raw, un-negated predicate. Indeterminate is a passthrough - it never participates in the
      // merge/exclude logic, it's only carried through for display once is_met is null.
      var effectiveMetExpr =
          criterion.exclude()
              ? "(NOT " + alias + "." + IS_MET_COLUMN + ")"
              : "(" + alias + "." + IS_MET_COLUMN + ")";

      String indeterminateExpr;
      if (columns.contains(IS_INDETERMINATE_COLUMN)) {
        indeterminateExpr = alias + "." + IS_INDETERMINATE_COLUMN;
      } else {
        indeterminateExpr = "CAST(NULL AS BOOLEAN)";
      }

      String noteExpr;
      if (columns.contains(RESULT_NOTE_COLUMN)) {
        noteExpr = alias + "." + RESULT_NOTE_COLUMN;
      } else {
        noteExpr = "CAST(NULL AS VARCHAR)";
      }

      selectColumns
          .append(",\n    ")
          .append(effectiveMetExpr)
          .append(" AS ")
          .append(alias)
          .append("_")
          .append(IS_MET_COLUMN)
          .append(",\n    ")
          .append(indeterminateExpr)
          .append(" AS ")
          .append(alias)
          .append("_")
          .append(IS_INDETERMINATE_COLUMN)
          .append(",\n    ")
          .append(noteExpr)
          .append(" AS ")
          .append(alias)
          .append("_")
          .append(RESULT_NOTE_COLUMN);
      terms.add(effectiveMetExpr);

      if (i == 0) {
        joins.append("FROM ").append(alias);
      } else {
        joins
            .append("\nLEFT JOIN ")
            .append(alias)
            .append(" ON ")
            .append(alias)
            .append(".patient_id = crit_0.patient_id");
      }
    }

    var overallExpr = String.join(" AND ", terms);

    // IS DISTINCT FROM FALSE = candidate or unknown; IS NOT DISTINCT FROM TRUE = confirmed only
    var whereFilter =
        requireAllCriteriaMet ? ") IS NOT DISTINCT FROM TRUE" : ") IS DISTINCT FROM FALSE";
    return ctes
        + "\nSELECT\n    crit_0.patient_id AS patient_id"
        + selectColumns
        + "\n"
        + joins
        + "\nWHERE ("
        + overallExpr
        + whereFilter;
  }

  /**
   * Reads one merged-query row directly off the {@link ResultSet} - deliberately not via a {@code
   * Map}-collecting row mapper, so that streaming callers (see {@link #evaluateAgainstTrino}) never
   * have more than the current row's data resident in memory. {@link JdbcUtils#getResultSetValue}
   * (rather than e.g. {@code rs.getBoolean}) is used so a SQL {@code NULL} comes through as Java
   * {@code null} instead of being coerced to {@code false}.
   */
  private PatientEligibilityResult toPatientEligibilityResult(
      ResultSet rs, List<EligibilityCriterion> criteria) throws SQLException {
    var patientId = (String) JdbcUtils.getResultSetValue(rs, rs.findColumn(PATIENT_ID_COLUMN));

    var outcomes = new ArrayList<CriterionOutcome>();
    for (var i = 0; i < criteria.size(); i++) {
      var criterion = criteria.get(i);
      var met =
          (Boolean)
              JdbcUtils.getResultSetValue(rs, rs.findColumn("crit_" + i + "_" + IS_MET_COLUMN));
      var indeterminate =
          Boolean.TRUE.equals(
              JdbcUtils.getResultSetValue(
                  rs, rs.findColumn("crit_" + i + "_" + IS_INDETERMINATE_COLUMN)));
      var note =
          (String)
              JdbcUtils.getResultSetValue(
                  rs, rs.findColumn("crit_" + i + "_" + RESULT_NOTE_COLUMN));
      outcomes.add(
          new CriterionOutcome(
              criterion.library(), criterion.displayText(), met, indeterminate, note));
    }

    return new PatientEligibilityResult(patientId, outcomes);
  }

  // ---- mixed/delegated fallback: resolve each criterion independently, merge in Java ----

  private void evaluateWithFallbackMerge(
      List<EligibilityCriterion> criteria, Consumer<PatientEligibilityResult> onResult) {
    var perCriterionResults = criteria.stream().map(this::resolveRawCriterionResults).toList();

    var allPatientIds = new LinkedHashSet<String>();
    perCriterionResults.forEach(m -> allPatientIds.addAll(m.keySet()));

    for (var patientId : allPatientIds) {
      var outcomes = new ArrayList<CriterionOutcome>();
      for (var i = 0; i < criteria.size(); i++) {
        var criterion = criteria.get(i);
        var raw = perCriterionResults.get(i).get(patientId);
        var met = raw == null || raw.met() == null ? null : (criterion.exclude() != raw.met());
        var indeterminate = raw != null && raw.indeterminate();
        var note = raw == null ? null : raw.note();
        outcomes.add(
            new CriterionOutcome(
                criterion.library(), criterion.displayText(), met, indeterminate, note));
      }

      var result = new PatientEligibilityResult(patientId, outcomes);
      var passes =
          requireAllCriteriaMet
              ? Boolean.TRUE.equals(result.overallMet())
              : !Boolean.FALSE.equals(result.overallMet());
      if (passes) {
        onResult.accept(result);
      }
    }
  }

  /** A criterion's raw (pre-exclude-negation) per-patient result, before merge. */
  private record RawCriterionResult(Boolean met, boolean indeterminate, String note) {}

  private Map<String, RawCriterionResult> resolveRawCriterionResults(
      EligibilityCriterion criterion) {
    if (isTrinoDirect(criterion)) {
      var rows = jdbcTemplate.queryForList(trinoSql(criterion));
      var results = new LinkedHashMap<String, RawCriterionResult>();
      for (var row : rows) {
        var patientId = (String) row.get(PATIENT_ID_COLUMN);
        var met = (Boolean) row.get(IS_MET_COLUMN);
        var indeterminate = Boolean.TRUE.equals(row.get(IS_INDETERMINATE_COLUMN));
        var note = (String) row.get(RESULT_NOTE_COLUMN);
        results.put(patientId, new RawCriterionResult(met, indeterminate, note));
      }
      return results;
    }

    return resolveRawResultsFromSqlOnFhir(criterion.library());
  }

  private Map<String, RawCriterionResult> resolveRawResultsFromSqlOnFhir(Library library) {
    log.info(
        "Delegating SQLQuery Library id={} to the sql-on-fhir server's $sqlquery-run operation",
        library.getId());

    var parameters = new Parameters();
    parameters.addParameter().setName("queryResource").setResource(library);
    parameters.addParameter().setName("_format").setValue(new CodeType("fhir"));

    var response =
        sqlOnFhirClient
            .operation()
            .onServer()
            .named("$sqlquery-run")
            .withParameters(parameters)
            .returnResourceType(Parameters.class)
            .execute();

    var results = new LinkedHashMap<String, RawCriterionResult>();
    for (var param : response.getParameter()) {
      if (!ROW_PARAMETER.equals(param.getName())) {
        continue;
      }

      String patientId = null;
      Boolean met = null;
      var indeterminate = false;
      String note = null;
      for (var part : param.getPart()) {
        if (PATIENT_ID_COLUMN.equals(part.getName())
            && part.getValue() instanceof PrimitiveType<?> pt) {
          patientId = pt.getValueAsString();
        } else if (IS_MET_COLUMN.equals(part.getName())
            && part.getValue() instanceof PrimitiveType<?> pt) {
          met = Boolean.parseBoolean(pt.getValueAsString());
        } else if (IS_INDETERMINATE_COLUMN.equals(part.getName())
            && part.getValue() instanceof PrimitiveType<?> pt) {
          indeterminate = Boolean.parseBoolean(pt.getValueAsString());
        } else if (RESULT_NOTE_COLUMN.equals(part.getName())
            && part.getValue() instanceof PrimitiveType<?> pt) {
          note = pt.getValueAsString();
        }
      }

      if (patientId != null) {
        results.put(patientId, new RawCriterionResult(met, indeterminate, note));
      }
    }

    return results;
  }
}
