package org.miracum.recruit.supersetlibrarysync.superset;

import java.time.Instant;
import java.util.Optional;

/**
 * One row of Superset's {@code saved_query} table, left-joined with its creating {@code ab_user},
 * as read directly from Superset's metadata database by {@link SupersetSavedQueryRepository} rather
 * than through Superset's REST API - the REST API's saved query endpoints only expose the
 * authenticated user's own saved queries, with no way to list everyone's.
 */
public record SavedQuery(
    long id,
    String label,
    String description,
    String schema,
    String sql,
    String username,
    String firstName,
    String lastName,
    Instant changedOn) {

  /**
   * {@code "firstName, lastName (username)"}, used by {@code SqlLibraryBuilder} as the Library's
   * author when the SQL has no {@code @author} annotation of its own. Empty when the saved query
   * has no known creator, e.g. its owning {@code ab_user} row no longer exists.
   */
  public Optional<String> defaultAuthor() {
    if (username == null) {
      return Optional.empty();
    }
    return Optional.of("%s, %s (%s)".formatted(firstName, lastName, username));
  }
}
