package org.miracum.recruit.querysqlonfhir;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Optional;
import java.util.stream.Collectors;
import org.hl7.fhir.r4.model.Attachment;
import org.hl7.fhir.r4.model.CodeType;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.PrimitiveType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Component;

/**
 * Evaluates a study's eligibility criteria (one per {@code Group.characteristic}, each backed by a
 * <a
 * href="https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/StructureDefinition-SQLQuery.html">SQLQuery</a>
 * Library) and merges the per-criterion results into one outcome per patient using three-valued
 * (Kleene) logic: a definite "not met" on any criterion dominates, and the overall result is
 * "unknown" only when nothing is definitely not met but at least one criterion's data is missing.
 *
 * <p>Every criterion Library's SQL must return exactly two columns - {@code patient_id} and {@code
 * met} (nullable boolean) - covering the full patient population, so that missing data reliably
 * produces a {@code null} row rather than a missing one.
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
  private static final String MET_COLUMN = "met";
  private static final String ROW_PARAMETER = "row";

  private final JdbcTemplate jdbcTemplate;
  private final IGenericClient sqlOnFhirClient;

  public SqlQueryExecutor(
      JdbcTemplate jdbcTemplate, @Qualifier("sqlOnFhirClient") IGenericClient sqlOnFhirClient) {
    this.jdbcTemplate = jdbcTemplate;
    this.sqlOnFhirClient = sqlOnFhirClient;
  }

  /**
   * Evaluates every criterion for every patient and returns one result per patient who is either a
   * candidate (all criteria met) or undecidable (nothing definitely not met, but at least one
   * unknown). Patients for whom at least one criterion is definitely not met are already excluded
   * from the returned list.
   */
  public List<PatientEligibilityResult> evaluateEligibility(List<EligibilityCriterion> criteria) {
    if (criteria.isEmpty()) {
      return List.of();
    }

    if (criteria.stream().allMatch(this::isTrinoDirect)) {
      return evaluateAgainstTrino(criteria);
    }

    log.warn(
        "Study has a mix of Trino-direct and sql-on-fhir-delegated criteria; falling back to"
            + " resolving each criterion independently and merging in application memory. This"
            + " does not scale as well as the all-Trino path - prefer keeping every criterion"
            + " Trino-direct where possible.");
    return evaluateWithFallbackMerge(criteria);
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

  private List<PatientEligibilityResult> evaluateAgainstTrino(List<EligibilityCriterion> criteria) {
    var sql = buildMergedQuery(criteria);
    log.info(
        "Running merged eligibility query for {} criteria against Trino:\n{}",
        criteria.size(),
        sql);

    var rows = jdbcTemplate.queryForList(sql);
    return rows.stream().map(row -> toPatientEligibilityResult(row, criteria)).toList();
  }

  /**
   * Wraps each criterion's already-standalone SQL as a CTE, joins them on {@code patient_id}
   * (anchored on the first criterion, which - per the two-column contract - already covers the full
   * patient population), and filters out definite non-matches via native Kleene {@code AND} pushed
   * down into the {@code WHERE} clause. See {@code docs/trino/eligibility-criteria-design.md} for
   * the full rationale.
   */
  private String buildMergedQuery(List<EligibilityCriterion> criteria) {
    var ctes = new StringBuilder();
    var selectColumns = new StringBuilder();
    var joins = new StringBuilder();
    var terms = new ArrayList<String>();

    for (var i = 0; i < criteria.size(); i++) {
      var criterion = criteria.get(i);
      var alias = "crit_" + i;

      ctes.append(i == 0 ? "WITH " : ",\n     ")
          .append(alias)
          .append(" AS (\n")
          .append(trinoSql(criterion))
          .append("\n)");

      // exclude negation is applied once, here, generically - criterion SQL always computes the
      // raw, un-negated predicate.
      var effectiveMetExpr =
          criterion.exclude() ? "(NOT " + alias + ".met)" : "(" + alias + ".met)";
      selectColumns
          .append(",\n    ")
          .append(effectiveMetExpr)
          .append(" AS ")
          .append(alias)
          .append("_met");
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

    return ctes
        + "\nSELECT\n    crit_0.patient_id AS patient_id"
        + selectColumns
        + "\n"
        + joins
        + "\nWHERE ("
        + overallExpr
        + ") IS DISTINCT FROM FALSE";
  }

  private PatientEligibilityResult toPatientEligibilityResult(
      Map<String, Object> row, List<EligibilityCriterion> criteria) {
    var patientId = (String) row.get(PATIENT_ID_COLUMN);

    var outcomes = new ArrayList<CriterionOutcome>();
    for (var i = 0; i < criteria.size(); i++) {
      var criterion = criteria.get(i);
      var met = (Boolean) row.get("crit_" + i + "_met");
      outcomes.add(new CriterionOutcome(criterion.library(), criterion.displayText(), met));
    }

    return new PatientEligibilityResult(patientId, outcomes);
  }

  // ---- mixed/delegated fallback: resolve each criterion independently, merge in Java ----

  private List<PatientEligibilityResult> evaluateWithFallbackMerge(
      List<EligibilityCriterion> criteria) {
    var perCriterionResults = criteria.stream().map(this::resolveRawCriterionResults).toList();

    var allPatientIds = new LinkedHashSet<String>();
    perCriterionResults.forEach(m -> allPatientIds.addAll(m.keySet()));

    var results = new ArrayList<PatientEligibilityResult>();
    for (var patientId : allPatientIds) {
      var outcomes = new ArrayList<CriterionOutcome>();
      for (var i = 0; i < criteria.size(); i++) {
        var criterion = criteria.get(i);
        var raw = perCriterionResults.get(i).get(patientId);
        var met = raw == null ? null : (criterion.exclude() != raw);
        outcomes.add(new CriterionOutcome(criterion.library(), criterion.displayText(), met));
      }

      var result = new PatientEligibilityResult(patientId, outcomes);
      if (!Boolean.FALSE.equals(result.overallMet())) {
        results.add(result);
      }
    }

    return results;
  }

  private Map<String, Boolean> resolveRawCriterionResults(EligibilityCriterion criterion) {
    if (isTrinoDirect(criterion)) {
      var rows = jdbcTemplate.queryForList(trinoSql(criterion));
      return rows.stream()
          .collect(
              Collectors.toMap(
                  row -> (String) row.get(PATIENT_ID_COLUMN),
                  row -> (Boolean) row.get(MET_COLUMN),
                  (a, b) -> a,
                  LinkedHashMap::new));
    }

    return resolveRawResultsFromSqlOnFhir(criterion.library());
  }

  private Map<String, Boolean> resolveRawResultsFromSqlOnFhir(Library library) {
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

    var results = new LinkedHashMap<String, Boolean>();
    for (var param : response.getParameter()) {
      if (!ROW_PARAMETER.equals(param.getName())) {
        continue;
      }

      String patientId = null;
      Boolean met = null;
      for (var part : param.getPart()) {
        if (PATIENT_ID_COLUMN.equals(part.getName())
            && part.getValue() instanceof PrimitiveType<?> pt) {
          patientId = pt.getValueAsString();
        } else if (MET_COLUMN.equals(part.getName())
            && part.getValue() instanceof PrimitiveType<?> pt) {
          met = Boolean.parseBoolean(pt.getValueAsString());
        }
      }

      if (patientId != null) {
        results.put(patientId, met);
      }
    }

    return results;
  }
}
