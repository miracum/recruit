package org.miracum.recruit.supersetlibrarysync.annotation;

import java.util.List;

/**
 * The result of parsing a SQL string's <a
 * href="https://build.fhir.org/ig/HL7/sql-on-fhir/en/StructureDefinition-SQLQuery.html#sql-annotations">SQL
 * on FHIR annotations</a>. Every single-valued field is {@code null} when its annotation wasn't
 * present; the repeatable ones ({@code author}, {@code param}, {@code relatedDependency}) default
 * to an empty list rather than {@code null}.
 */
public record ParsedSqlAnnotations(
    String name,
    String title,
    String description,
    String version,
    String status,
    String publisher,
    List<String> authors,
    List<SqlParameter> parameters,
    List<RelatedDependency> relatedDependencies) {

  /**
   * {@code true} if none of the 7 recognized annotations were found - used by {@code SyncService}
   * as a safety net so a saved query that isn't actually meant to become a Library (e.g. mistagged
   * or ad hoc scratch SQL) isn't synced just because it matched the configured Superset tag.
   */
  public boolean isEmpty() {
    return name == null
        && title == null
        && description == null
        && version == null
        && status == null
        && publisher == null
        && authors.isEmpty()
        && parameters.isEmpty()
        && relatedDependencies.isEmpty();
  }
}
