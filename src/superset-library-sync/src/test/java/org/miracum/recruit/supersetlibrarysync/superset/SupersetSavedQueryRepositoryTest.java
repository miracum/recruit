package org.miracum.recruit.supersetlibrarysync.superset;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import java.sql.ResultSet;
import java.sql.Timestamp;
import java.time.Instant;
import java.util.Calendar;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.RowMapper;

class SupersetSavedQueryRepositoryTest {

  private final JdbcTemplate jdbcTemplate = mock(JdbcTemplate.class);
  private final SupersetSavedQueryRepository sut = new SupersetSavedQueryRepository(jdbcTemplate);

  @Test
  @SuppressWarnings("unchecked")
  void findAll_mapsEachRowToSavedQuery() throws Exception {
    var changedOn = Instant.parse("2024-03-05T10:15:30Z");
    var expected =
        new SavedQuery(
            42,
            "My Query",
            "A saved query",
            "public",
            "SELECT 1",
            "jdoe",
            "Jane",
            "Doe",
            changedOn);

    var rowMapperCaptor = ArgumentCaptor.forClass(RowMapper.class);
    when(jdbcTemplate.query(anyString(), rowMapperCaptor.capture())).thenReturn(List.of(expected));

    var result = sut.findAll();

    assertThat(result).containsExactly(expected);

    var resultSet = mock(ResultSet.class);
    when(resultSet.getLong("id")).thenReturn(42L);
    when(resultSet.getString("label")).thenReturn("My Query");
    when(resultSet.getString("description")).thenReturn("A saved query");
    when(resultSet.getString("schema")).thenReturn("public");
    when(resultSet.getString("sql")).thenReturn("SELECT 1");
    when(resultSet.getString("username")).thenReturn("jdoe");
    when(resultSet.getString("first_name")).thenReturn("Jane");
    when(resultSet.getString("last_name")).thenReturn("Doe");
    when(resultSet.getTimestamp(eq("changed_on"), any(Calendar.class)))
        .thenReturn(Timestamp.from(changedOn));

    assertThat(rowMapperCaptor.getValue().mapRow(resultSet, 0)).isEqualTo(expected);
  }
}
