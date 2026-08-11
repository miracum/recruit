package org.miracum.recruit.queryfhirtrino;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Locale;
import java.util.Optional;
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
 * Executes the SQL query encoded in a <a
 * href="https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/StructureDefinition-SQLQuery.html">SQLQuery</a>
 * FHIR Library resource.
 *
 * <p>If the Library has no {@code relatedArtifact} (i.e. it doesn't depend on any ViewDefinition)
 * and carries a {@code application/sql;dialect=trino} content entry, the query is executed directly
 * against the configured Trino database. Otherwise, the Library is forwarded as-is to a configured
 * sql-on-fhir server's <a
 * href="https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/OperationDefinition-SQLQueryRun.html">$sqlquery-run</a>
 * operation.
 */
@Component
public class SqlQueryExecutor {

  private static final Logger log = LoggerFactory.getLogger(SqlQueryExecutor.class);

  private static final String TRINO_SQL_CONTENT_TYPE = "application/sql;dialect=trino";
  private static final String PATIENT_ID_COLUMN = "patient_id";
  private static final String ROW_PARAMETER = "row";

  private final JdbcTemplate jdbcTemplate;
  private final IGenericClient sqlOnFhirClient;

  public SqlQueryExecutor(
      JdbcTemplate jdbcTemplate, @Qualifier("sqlOnFhirClient") IGenericClient sqlOnFhirClient) {
    this.jdbcTemplate = jdbcTemplate;
    this.sqlOnFhirClient = sqlOnFhirClient;
  }

  public List<String> resolvePatientIds(Library library) {
    var trinoContent = findTrinoDialectContent(library);

    if (trinoContent.isPresent() && !library.hasRelatedArtifact()) {
      return runAgainstTrino(trinoContent.get());
    }

    return runAgainstSqlOnFhirServer(library);
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

  private List<String> runAgainstTrino(Attachment content) {
    var sql = new String(content.getData(), StandardCharsets.UTF_8);

    log.info("Running SQLQuery Library content directly against Trino: {}", sql);

    var result = jdbcTemplate.queryForList(sql);
    return result.stream().map(row -> (String) row.get(PATIENT_ID_COLUMN)).distinct().toList();
  }

  private List<String> runAgainstSqlOnFhirServer(Library library) {
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

    return extractPatientIds(response);
  }

  private List<String> extractPatientIds(Parameters response) {
    return response.getParameter().stream()
        .filter(param -> ROW_PARAMETER.equals(param.getName()))
        .flatMap(row -> row.getPart().stream())
        .filter(part -> PATIENT_ID_COLUMN.equals(part.getName()))
        .filter(part -> part.getValue() instanceof PrimitiveType<?>)
        .map(part -> ((PrimitiveType<?>) part.getValue()).getValueAsString())
        .distinct()
        .toList();
  }
}
