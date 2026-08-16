package org.miracum.recruit.supersetlibrarysync.superset;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import java.util.List;

/**
 * The subset of a Superset <a
 * href="https://superset.apache.org/developer-docs/api/#tag/Queries/operation/get_list_api_v1_saved_query_get">saved
 * query</a> this module needs. Field names follow the {@code saved_query} REST API model as of
 * Superset's current OpenAPI spec; verify against the actual target instance if it's on an older
 * version, since this is a REST-model detail Superset doesn't treat as a stable public contract.
 */
@JsonIgnoreProperties(ignoreUnknown = true)
public record SavedQuery(
    long id, String label, String description, String schema, String sql, List<Tag> tags) {

  @JsonIgnoreProperties(ignoreUnknown = true)
  public record Tag(long id, String name) {}

  public boolean hasTag(String name) {
    return tags != null && tags.stream().anyMatch(tag -> tag.name().equals(name));
  }
}
