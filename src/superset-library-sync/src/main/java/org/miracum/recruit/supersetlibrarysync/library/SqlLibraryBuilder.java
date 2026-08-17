package org.miracum.recruit.supersetlibrarysync.library;

import com.github.slugify.Slugify;
import io.github.dizuker.tofhir.IdUtils;
import io.github.miracum.recruit.Recruit;
import java.nio.charset.StandardCharsets;
import java.util.List;
import org.hl7.fhir.r4.model.Attachment;
import org.hl7.fhir.r4.model.Coding;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.Enumerations;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.ParameterDefinition;
import org.hl7.fhir.r4.model.RelatedArtifact;
import org.hl7.fhir.r4.model.StringType;
import org.miracum.recruit.supersetlibrarysync.annotation.ParsedSqlAnnotations;
import org.miracum.recruit.supersetlibrarysync.superset.SavedQuery;
import org.miracum.recruit.supersetlibrarysync.superset.SupersetProperties;
import org.springframework.stereotype.Component;

/**
 * Maps a {@link SavedQuery} and its {@link ParsedSqlAnnotations} to a {@code sql-query} {@link
 * Library}, applying the SQL on FHIR IG's <a
 * href="https://build.fhir.org/ig/HL7/sql-on-fhir/en/StructureDefinition-SQLQuery.html#sql-annotations">"Builders
 * SHALL/SHOULD"</a> tooling rules.
 */
@Component
public class SqlLibraryBuilder {

  // kept identical to query-sql-on-fhir's PollForStudies.SQL_QUERY_LIBRARY_TYPE_SYSTEM, the
  // module that actually searches for/consumes these Library resources - note this is *not* the
  // CodeSystem URL of the IG's current published build
  // (http://hl7.org/fhir/uv/sql-on-fhir/CodeSystem/LibraryTypesCodes); the two need to be
  // reconciled against whichever IG version the target FHIR server actually expects.
  private static final String LIBRARY_TYPE_SYSTEM =
      "https://sql-on-fhir.org/ig/CodeSystem/LibraryTypesCodes";
  private static final String LIBRARY_TYPE_CODE = "sql-query";
  private static final String SQL_TEXT_EXTENSION_URL =
      "http://hl7.org/fhir/uv/sql-on-fhir/StructureDefinition/sql-text";
  private static final String DEFAULT_STATUS = "draft";

  private final String sqlDialect;
  private final Slugify slugify = Slugify.builder().build();

  public SqlLibraryBuilder(SupersetProperties properties) {
    this.sqlDialect = properties.sqlDialect();
  }

  /**
   * Builds a {@code sql-query} Library. {@code annotations.name()} must be non-null - it's what the
   * identifier (and, in turn, the deterministic id below) is derived from, so {@code SyncService}
   * is expected to have already skipped any saved query without one.
   */
  public Library build(SavedQuery savedQuery, ParsedSqlAnnotations annotations) {
    var library = new Library();

    // a slug of @name, not the saved query's own Superset id: it's a more stable, human-legible
    // identity that survives the saved query being deleted and recreated, e.g. as part of
    // migrating it between Superset instances.
    var identifier =
        library
            .addIdentifier()
            .setSystem(Recruit.NamingSystems.SupersetSyncedEligibilityLibraryId.uri())
            .setValue(slugify.slugify(annotations.name()));
    // a deterministic id (rather than a server-assigned one) lets SyncService upsert each
    // Library via a plain PUT to Library/<id> instead of a conditional PUT search, and makes the
    // id independently reproducible from the identifier alone, e.g. for consumers that only have
    // the saved query's @name annotation on hand.
    library.setId(IdUtils.fromIdentifier(identifier));

    library.setName(resolveName(savedQuery, annotations));
    library.setTitle(annotations.title() != null ? annotations.title() : savedQuery.label());
    library.setStatus(
        Enumerations.PublicationStatus.fromCode(
            annotations.status() != null ? annotations.status() : DEFAULT_STATUS));

    // Library.date (not Resource.meta.lastUpdated, which most FHIR servers overwrite on every
    // write with the actual server-side write time) is the IG's "when the library's substantive
    // content last changed" field - the closest FHIR analogue to Superset's changed_on. Built from
    // Instant#toString() rather than Library#setDate(Date), which would stamp the offset of
    // whatever timezone this JVM happens to run in instead of the UTC one changed_on is actually
    // in.
    if (savedQuery.changedOn() != null) {
      library.setDateElement(new DateTimeType(savedQuery.changedOn().toString()));
    }

    var description =
        annotations.description() != null ? annotations.description() : savedQuery.description();
    if (description != null) {
      library.setDescription(description);
    }
    if (annotations.version() != null) {
      library.setVersion(annotations.version());
    }
    if (annotations.publisher() != null) {
      library.setPublisher(annotations.publisher());
    }

    for (var author : resolveAuthors(savedQuery, annotations)) {
      library.addAuthor().setName(author);
    }

    library.getType().addCoding(new Coding(LIBRARY_TYPE_SYSTEM, LIBRARY_TYPE_CODE, null));

    addContent(library, savedQuery.sql());

    for (var dependency : annotations.relatedDependencies()) {
      var relatedArtifact =
          library
              .addRelatedArtifact()
              .setType(RelatedArtifact.RelatedArtifactType.DEPENDSON)
              .setUrl(dependency.url());
      if (dependency.label() != null) {
        relatedArtifact.setLabel(dependency.label());
      }
    }

    for (var parameter : annotations.parameters()) {
      var parameterDefinition =
          library
              .addParameter()
              .setName(parameter.name())
              .setUse(ParameterDefinition.ParameterUse.IN)
              .setType(parameter.type());
      if (parameter.description() != null) {
        parameterDefinition.setDocumentation(parameter.description());
      }
    }

    return library;
  }

  /**
   * Per the IG: a default {@code application/sql} attachment (SHALL, no dialect commitment) plus,
   * when configured, a second {@code application/sql;dialect=<sqlDialect>} attachment - both
   * carrying the same raw SQL text, comments included: a SQL engine ignores comments, and the IG
   * doesn't ask for them to be stripped. Each attachment also carries the IG's {@code sql-text}
   * extension with the same text in plain (non-base64) form.
   */
  private void addContent(Library library, String sql) {
    library.addContent(buildAttachment("application/sql", sql));

    if (sqlDialect != null && !sqlDialect.isBlank()) {
      library.addContent(buildAttachment("application/sql;dialect=" + sqlDialect, sql));
    }
  }

  private static Attachment buildAttachment(String contentType, String sql) {
    var attachment =
        new Attachment().setContentType(contentType).setData(sql.getBytes(StandardCharsets.UTF_8));
    attachment.addExtension(SQL_TEXT_EXTENSION_URL, new StringType(sql));
    return attachment;
  }

  /**
   * The {@code @author} annotation(s) if at least one is present, else the saved query's creator
   * (see {@link SavedQuery#defaultAuthor()}) as a single fallback author, else none at all.
   */
  private static List<String> resolveAuthors(
      SavedQuery savedQuery, ParsedSqlAnnotations annotations) {
    if (!annotations.authors().isEmpty()) {
      return annotations.authors();
    }
    return savedQuery.defaultAuthor().map(List::of).orElseGet(List::of);
  }

  /**
   * {@code @name} if present, else a name derived from {@code @title}, else one derived from the
   * saved query's Superset label - the IG's "infer name from filename" SHOULD rule has no filename
   * here, so the saved query's label is the closest analogue.
   */
  private static String resolveName(SavedQuery savedQuery, ParsedSqlAnnotations annotations) {
    if (annotations.name() != null) {
      return annotations.name();
    }
    if (annotations.title() != null) {
      return toPascalCaseName(annotations.title());
    }
    return toPascalCaseName(savedQuery.label());
  }

  private static String toPascalCaseName(String text) {
    var builder = new StringBuilder();
    for (var word : text.split("[^A-Za-z0-9]+")) {
      if (word.isEmpty()) {
        continue;
      }
      builder.append(Character.toUpperCase(word.charAt(0))).append(word.substring(1));
    }
    return builder.isEmpty() ? "UnnamedSavedQuery" : builder.toString();
  }
}
