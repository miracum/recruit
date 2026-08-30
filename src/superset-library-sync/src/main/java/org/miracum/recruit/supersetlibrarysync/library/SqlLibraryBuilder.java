package org.miracum.recruit.supersetlibrarysync.library;

import com.github.slugify.Slugify;
import io.github.dizuker.tofhir.IdUtils;
import io.github.dizuker.tofhir.TransactionBuilder;
import io.github.miracum.recruit.Recruit;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.regex.Pattern;
import org.hl7.fhir.r4.model.Attachment;
import org.hl7.fhir.r4.model.Bundle;
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
 * SHALL/SHOULD"</a> tooling rules, and wraps it in a single-entry {@link Bundle} (via {@code
 * TransactionBuilder}) with a {@code PUT} request keyed by the Library's own deterministic id - a
 * self-contained upsert that {@code SyncService} either merges into its aggregate {@code batch}
 * FHIR submission bundle or publishes standalone (e.g. to Kafka), without either consumer needing
 * to know how to construct the entry itself.
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

  // mirrors SqlAnnotationParser.ANNOTATION_LINE - kept separate since this class only needs to
  // detect *whether* a leading comment carries an annotation, not parse its @key/value pairs out.
  private static final Pattern ANNOTATION_LINE =
      Pattern.compile("^\\s*@\\w+\\s*:\\s*\\S", Pattern.MULTILINE);

  private final String sqlDialect;
  private final Slugify slugify = Slugify.builder().build();

  public SqlLibraryBuilder(SupersetProperties properties) {
    this.sqlDialect = properties.sqlDialect();
  }

  /**
   * Builds a {@code sql-query} Library, wrapped in a single-entry PUT {@link Bundle} (see {@link
   * #toPutBundle}). {@code annotations.name()} must be non-null - it's what the identifier (and, in
   * turn, the deterministic id below) is derived from, so {@code SyncService} is expected to have
   * already skipped any saved query without one.
   */
  public Bundle build(SavedQuery savedQuery, ParsedSqlAnnotations annotations) {
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

    return toPutBundle(library);
  }

  /**
   * Wraps the Library in a single-entry PUT {@link Bundle} keyed by the Library's own deterministic
   * id rather than a server-assigned one, via {@code to-fhir}'s {@code TransactionBuilder} -
   * produced once here so every consumer (FHIR submission, Kafka publishing) gets the same
   * self-contained upsert without needing to build the entry itself.
   */
  private static Bundle toPutBundle(Library library) {
    return new TransactionBuilder().withId(library.getId()).addEntry(library).build();
  }

  /**
   * Per the IG: a default {@code application/sql} attachment (SHALL, no dialect commitment) plus,
   * when configured, a second {@code application/sql;dialect=<sqlDialect>} attachment - both
   * carrying the SQL text with its leading SQL on FHIR annotation comments (the ones {@link
   * org.miracum.recruit.supersetlibrarysync.annotation.SqlAnnotationParser} already parsed into the
   * Library's own elements) removed, since that metadata would otherwise be duplicated verbatim in
   * the stored query. Any other, non-annotation comments are left untouched. Each attachment also
   * carries the IG's {@code sql-text} extension with the same text in plain (non-base64) form.
   */
  private void addContent(Library library, String sql) {
    var content = stripLeadingAnnotationComments(sql);
    library.addContent(buildAttachment("application/sql", content));

    if (sqlDialect != null && !sqlDialect.isBlank()) {
      library.addContent(buildAttachment("application/sql;dialect=" + sqlDialect, content));
    }
  }

  /**
   * Drops leading {@code /* @key: value *&#47;} block and/or {@code -- @key: value} line comments
   * from the front of the SQL. A leading comment is only dropped if it actually contains an {@code
   * @key: value} annotation line; the first comment (or other content) that doesn't is left in
   * place, along with everything after it.
   */
  private static String stripLeadingAnnotationComments(String sql) {
    var pos = 0;
    for (var next = leadingAnnotationCommentEnd(sql, pos);
        next >= 0;
        next = leadingAnnotationCommentEnd(sql, pos)) {
      pos = next;
    }
    return sql.substring(pos).stripLeading();
  }

  /**
   * If an annotation comment starts at {@code pos} (skipping any leading whitespace), returns the
   * index right after it; otherwise returns {@code -1}.
   */
  private static int leadingAnnotationCommentEnd(String sql, int pos) {
    var length = sql.length();
    var contentStart = pos;
    while (contentStart < length && Character.isWhitespace(sql.charAt(contentStart))) {
      contentStart++;
    }

    String commentBody;
    int commentEnd;
    if (sql.startsWith("/*", contentStart)) {
      var close = sql.indexOf("*/", contentStart + 2);
      if (close < 0) {
        return -1;
      }
      commentBody = sql.substring(contentStart + 2, close);
      commentEnd = close + 2;
    } else if (sql.startsWith("--", contentStart)) {
      var eol = sql.indexOf('\n', contentStart + 2);
      commentEnd = eol < 0 ? length : eol;
      commentBody = sql.substring(contentStart + 2, commentEnd);
    } else {
      return -1;
    }

    return ANNOTATION_LINE.matcher(commentBody).find() ? commentEnd : -1;
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
