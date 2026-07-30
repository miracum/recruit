package org.miracum.recruit.queryfhirtrino;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.RETURNS_DEEP_STUBS;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import java.nio.charset.StandardCharsets;
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

  private static Library libraryWithContent(String contentType, String sql) {
    var library = new Library();
    library.addContent().setContentType(contentType).setData(sql.getBytes(StandardCharsets.UTF_8));
    return library;
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

  @Test
  void resolvePatientIds_withTrinoDialectContentAndNoRelatedArtifact_runsDirectlyAgainstTrino() {
    var library = libraryWithContent("application/sql;dialect=trino", "SELECT patient_id FROM x");

    when(jdbcTemplate.queryForList("SELECT patient_id FROM x"))
        .thenReturn(
            List.of(
                Map.of("patient_id", "pat-1"),
                Map.of("patient_id", "pat-2"),
                Map.of("patient_id", "pat-1")));

    var result = sut.resolvePatientIds(library);

    assertThat(result).containsExactlyInAnyOrder("pat-1", "pat-2");
    verifyNoInteractions(sqlOnFhirClient);
  }

  @Test
  void resolvePatientIds_withWhitespaceAndMixedCaseTrinoDialect_stillRunsDirectlyAgainstTrino() {
    var library = libraryWithContent("application/sql; Dialect=TRINO", "SELECT patient_id FROM x");

    when(jdbcTemplate.queryForList("SELECT patient_id FROM x"))
        .thenReturn(List.of(Map.of("patient_id", "pat-1")));

    var result = sut.resolvePatientIds(library);

    assertThat(result).containsExactly("pat-1");
    verifyNoInteractions(sqlOnFhirClient);
  }

  @Test
  void resolvePatientIds_withRelatedArtifact_delegatesToSqlOnFhirServerInstead() {
    var library = libraryWithContent("application/sql;dialect=trino", "SELECT patient_id FROM x");
    library
        .addRelatedArtifact()
        .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
        .setLabel("patients")
        .setResource("ViewDefinition/patients");

    stubSqlOnFhirResponse(parametersWithRows(List.of(Map.of("patient_id", "pat-1"))));

    var result = sut.resolvePatientIds(library);

    assertThat(result).containsExactly("pat-1");
    verifyNoInteractions(jdbcTemplate);
  }

  @Test
  void resolvePatientIds_withoutTrinoDialectContent_delegatesToSqlOnFhirServer() {
    var library = libraryWithContent("application/sql", "SELECT patient_id FROM x");

    stubSqlOnFhirResponse(
        parametersWithRows(List.of(Map.of("patient_id", "pat-1"), Map.of("patient_id", "pat-2"))));

    var result = sut.resolvePatientIds(library);

    assertThat(result).containsExactlyInAnyOrder("pat-1", "pat-2");
    verifyNoInteractions(jdbcTemplate);
  }

  @Test
  void resolvePatientIds_sendsTheOriginalLibraryAsTheQueryResourceParameter() {
    var library = libraryWithContent("application/sql", "SELECT patient_id FROM x");

    var captor = ArgumentCaptor.forClass(Parameters.class);
    when(sqlOnFhirClient
            .operation()
            .onServer()
            .named("$sqlquery-run")
            .withParameters(captor.capture())
            .returnResourceType(Parameters.class)
            .execute())
        .thenReturn(parametersWithRows(List.of()));

    sut.resolvePatientIds(library);

    var sentQueryResource =
        captor.getValue().getParameter().stream()
            .filter(p -> "queryResource".equals(p.getName()))
            .findFirst()
            .orElseThrow()
            .getResource();

    assertThat(sentQueryResource).isSameAs(library);
  }

  @Test
  void resolvePatientIds_ignoresNonPatientIdPartsInTheResponse() {
    var library = libraryWithContent("application/sql", "SELECT patient_id FROM x");

    var row = new Parameters.ParametersParameterComponent().setName("row");
    row.addPart().setName("patient_id").setValue(new StringType("pat-1"));
    row.addPart().setName("some_other_column").setValue(new StringType("ignored"));
    var response = new Parameters();
    response.addParameter(row);

    stubSqlOnFhirResponse(response);

    var result = sut.resolvePatientIds(library);

    assertThat(result).containsExactly("pat-1");
  }

  private static Parameters parametersWithRows(List<Map<String, String>> rows) {
    var parameters = new Parameters();
    for (var row : rows) {
      var rowParam = new Parameters.ParametersParameterComponent().setName("row");
      row.forEach(
          (name, value) -> rowParam.addPart().setName(name).setValue(new StringType(value)));
      parameters.addParameter(rowParam);
    }
    return parameters;
  }
}
