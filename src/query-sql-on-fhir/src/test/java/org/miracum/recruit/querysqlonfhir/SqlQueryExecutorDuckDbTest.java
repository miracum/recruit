package org.miracum.recruit.querysqlonfhir;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verifyNoInteractions;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import com.zaxxer.hikari.HikariConfig;
import com.zaxxer.hikari.HikariDataSource;
import java.net.URISyntaxException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import org.hl7.fhir.r4.model.Library;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.jdbc.core.JdbcTemplate;

/**
 * Runs {@link SqlQueryExecutor}'s generated merged and funnel queries against a real, in-memory
 * DuckDB database over FHIR NDJSON files - unlike {@link SqlQueryExecutorTest}, which only checks
 * the generated SQL's shape against a mocked {@link JdbcTemplate}. The tables are set up exactly as
 * in a deployment: as views created by Hikari's {@code connection-init-sql} on every new pooled
 * connection, since each {@code jdbc:duckdb:} connection is its own, empty in-memory database.
 *
 * <p>The fixture patients in {@code src/test/resources/duckdb/} cover every outcome of the sample
 * study's three criteria (bronchitis, age over 18, no prior sepsis): all met, excluded by sepsis,
 * excluded by age, no bronchitis, and an unknown age (no birthDate).
 */
class SqlQueryExecutorDuckDbTest {

  private static final String ADULT_BRONCHITIS = "0b6f6a4e-1111-4a8e-9f00-000000000001";
  private static final String UNKNOWN_AGE_BRONCHITIS = "0b6f6a4e-1111-4a8e-9f00-000000000005";

  private static final String SQL_BRONCHITIS = conditionCriterionSql("10509002");
  private static final String SQL_SEPSIS = conditionCriterionSql("91302008");
  private static final String SQL_AGE_OVER_18 =
      """
      SELECT
          patient.id AS patient_id,
          cast(NULL AS BOOLEAN) AS is_indeterminate,
          cast(NULL AS VARCHAR) AS result_note,
          date_sub('year', CAST(patient.birthDate AS DATE), current_date) > 18 AS is_met
      FROM fhir.default.patient AS patient
      """;

  private final IGenericClient sqlOnFhirClient = mock(IGenericClient.class);

  private HikariDataSource dataSource;
  private SqlQueryExecutor sut;

  private static String conditionCriterionSql(String snomedCode) {
    return """
        SELECT
            patient.id AS patient_id,
            EXISTS (
                SELECT 1
                FROM
                    fhir.default.condition,
                    UNNEST(condition.code.coding) AS u(condition_coding)
                WHERE
                    condition.subject.reference = CONCAT('Patient/', patient.id)
                    AND condition_coding.system = 'http://snomed.info/sct'
                    AND condition_coding.code IN ('%s')
            ) AS is_met,
            CAST(NULL AS BOOLEAN) AS is_indeterminate,
            CAST(NULL AS VARCHAR) AS result_note
        FROM fhir.default.patient AS patient
        """
        .formatted(snomedCode);
  }

  private static Library duckDbLibrary(String sql) {
    var library = new Library();
    library
        .addContent()
        .setContentType("application/sql;dialect=duckdb")
        .setData(sql.getBytes(StandardCharsets.UTF_8));
    return library;
  }

  @BeforeEach
  void setUp() throws URISyntaxException {
    var fixtures =
        Path.of(Objects.requireNonNull(getClass().getResource("/duckdb")).toURI()).toString();

    var config = new HikariConfig();
    config.setJdbcUrl("jdbc:duckdb:");
    // more than one connection, to make sure every pooled connection gets the views, not just the
    // first one
    config.setMinimumIdle(2);
    config.setMaximumPoolSize(2);
    config.setConnectionInitSql(
        """
        SET autoinstall_known_extensions = false;
        SET autoload_known_extensions = false;
        ATTACH ':memory:' AS fhir;
        CREATE SCHEMA fhir."default";
        CREATE VIEW fhir."default".patient AS SELECT * FROM read_json('%1$s/Patient.ndjson');
        CREATE VIEW fhir."default".condition AS SELECT * FROM read_json('%1$s/Condition.ndjson');
        USE fhir."default";
        """
            .formatted(fixtures));
    dataSource = new HikariDataSource(config);

    sut = new SqlQueryExecutor(new JdbcTemplate(dataSource), sqlOnFhirClient, false, "duckdb");
  }

  @AfterEach
  void tearDown() {
    dataSource.close();
  }

  private static List<EligibilityCriterion> sampleStudyCriteria() {
    return List.of(
        new EligibilityCriterion(duckDbLibrary(SQL_BRONCHITIS), "Bronchitis diagnosis", false),
        new EligibilityCriterion(duckDbLibrary(SQL_AGE_OVER_18), "Age > 18 years", false),
        new EligibilityCriterion(duckDbLibrary(SQL_SEPSIS), "No prior sepsis", true));
  }

  private static Boolean overallMetOf(List<PatientEligibilityResult> results, String patientId) {
    return results.stream()
        .filter(r -> r.patientId().equals(patientId))
        .findFirst()
        .orElseThrow()
        .overallMet();
  }

  @Test
  void evaluateEligibility_returnsCandidatesAndUnknownsWithPatientIdsAsStrings() {
    var results = new ArrayList<PatientEligibilityResult>();

    sut.evaluateEligibility(sampleStudyCriteria(), results::add);

    // DuckDB infers the UUID-shaped ids as UUID - they must still arrive as plain strings
    assertThat(results)
        .extracting(PatientEligibilityResult::patientId)
        .containsExactlyInAnyOrder(ADULT_BRONCHITIS, UNKNOWN_AGE_BRONCHITIS);

    assertThat(overallMetOf(results, ADULT_BRONCHITIS)).isTrue();
    assertThat(overallMetOf(results, UNKNOWN_AGE_BRONCHITIS)).isNull();

    verifyNoInteractions(sqlOnFhirClient);
  }

  @Test
  void computeFunnel_returnsCascadingCounts() {
    var funnel = sut.computeFunnel(sampleStudyCriteria(), "SELECT COUNT(*) FROM Patient");

    assertThat(funnel.totalPopulation()).isEqualTo(5);
    // bronchitis: all but adult-healthy; age: drops child-bronchitis (unknown age stays); no
    // sepsis: drops adult-bronchitis-sepsis
    assertThat(funnel.steps())
        .extracting(FunnelResult.Step::remainingCount)
        .containsExactly(4L, 3L, 2L);
  }
}
