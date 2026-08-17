package org.miracum.recruit.supersetlibrarysync.superset;

import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.Calendar;
import java.util.List;
import java.util.TimeZone;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Component;

/**
 * Reads every SQL Lab saved query directly from Superset's own metadata database, rather than
 * through its REST API - the REST API's {@code saved_query} list/get endpoints are scoped to the
 * authenticated user's own saved queries, with no way to see everyone's, which defeats the point of
 * finding every annotated saved query across an instance regardless of who created it.
 */
@Component
public class SupersetSavedQueryRepository {

  private static final String FIND_ALL_SQL =
      """
      SELECT sq.id, sq.label, sq.schema, sq.sql, sq.description, d.database_name, u.username,
        u.first_name, u.last_name, sq.created_on, sq.changed_on
      FROM saved_query sq
      LEFT JOIN ab_user u ON u.id = sq.created_by_fk
      LEFT JOIN dbs d ON d.id = sq.db_id
      ORDER BY sq.changed_on DESC
      """;

  private static final TimeZone UTC = TimeZone.getTimeZone("UTC");

  private final JdbcTemplate jdbcTemplate;

  public SupersetSavedQueryRepository(JdbcTemplate jdbcTemplate) {
    this.jdbcTemplate = jdbcTemplate;
  }

  /** Every saved query on the Superset instance, regardless of who created it. */
  public List<SavedQuery> findAll() {
    return jdbcTemplate.query(FIND_ALL_SQL, SupersetSavedQueryRepository::mapRow);
  }

  private static SavedQuery mapRow(ResultSet rs, int rowNum) throws SQLException {
    // Superset stores changed_on as a naive UTC timestamp (Flask-AppBuilder's AuditMixin uses
    // datetime.utcnow()). Without an explicit UTC Calendar, getTimestamp() would instead
    // interpret that naive value in this JVM's default timezone, silently shifting it.
    var changedOn = rs.getTimestamp("changed_on", Calendar.getInstance(UTC));
    return new SavedQuery(
        rs.getLong("id"),
        rs.getString("label"),
        rs.getString("description"),
        rs.getString("schema"),
        rs.getString("sql"),
        rs.getString("username"),
        rs.getString("first_name"),
        rs.getString("last_name"),
        changedOn != null ? changedOn.toInstant() : null);
  }
}
