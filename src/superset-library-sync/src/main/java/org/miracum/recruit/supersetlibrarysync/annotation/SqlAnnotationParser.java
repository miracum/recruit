package org.miracum.recruit.supersetlibrarysync.annotation;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import java.util.regex.Pattern;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

/**
 * Extracts the <a
 * href="https://build.fhir.org/ig/HL7/sql-on-fhir/en/StructureDefinition-SQLQuery.html#sql-annotations">SQL
 * on FHIR IG's "SQL Annotations"</a> from a SQL string's block ({@code /* *&#47;}) and line ({@code
 * --}) comments - {@code @key: value}, one per comment line. Reimplements the IG's "Builders
 * SHALL"/"SHOULD" tooling rules directly against the raw text rather than via a real SQL grammar,
 * since a grammar-aware parser like JSqlParser would strip or ignore exactly the comments this
 * needs to read.
 *
 * <p>Recognizes exactly the 7 annotations the IG defines - {@code @name}, {@code @title}, {@code
 * @description}, {@code @version}, {@code @status}, {@code @author} (repeatable), {@code
 * @publisher}, {@code @param} (repeatable), {@code @relatedDependency} (repeatable) - and logs a
 * warning for anything else, per the IG's "SHOULD ... warn on unrecognized annotations" rule.
 *
 * <p>Known limitation: comment detection is regex-based, not a real SQL tokenizer, so a {@code --}
 * or {@code /*} that happens to appear inside a string literal would be misread as a comment. In
 * practice this hasn't mattered since annotations live in a dedicated leading comment block, not
 * interleaved with the query body.
 */
@Component
public class SqlAnnotationParser {

  private static final Logger log = LoggerFactory.getLogger(SqlAnnotationParser.class);

  private static final Pattern BLOCK_COMMENT = Pattern.compile("/\\*(.*?)\\*/", Pattern.DOTALL);
  private static final Pattern LINE_COMMENT = Pattern.compile("--([^\\r\\n]*)");
  private static final Pattern ANNOTATION_LINE = Pattern.compile("^\\s*@(\\w+)\\s*:\\s*(.+?)\\s*$");
  private static final Pattern PARAM_VALUE = Pattern.compile("^(\\S+)\\s+(\\S+)(?:\\s+(.+))?$");
  private static final Pattern RELATED_DEPENDENCY_VALUE =
      Pattern.compile("^(\\S+)(?:\\s+as\\s+(\\S+))?$");
  private static final Pattern VALID_LABEL = Pattern.compile("^[a-zA-Z_][a-zA-Z0-9_]*$");

  // what Library.parameter.type / @param's second token may be - a subset of FHIR's primitive
  // types, matching the IG's own @param examples ("patient_id string", "from_date date").
  private static final Set<String> ALLOWED_PARAM_TYPES =
      Set.of("string", "integer", "decimal", "boolean", "date", "dateTime");

  private static final Set<String> SINGLE_VALUED_KEYS =
      Set.of("name", "title", "description", "version", "status", "publisher");

  public ParsedSqlAnnotations parse(String sql) {
    var singleValued = new LinkedHashMap<String, String>();
    var authors = new ArrayList<String>();
    var parameters = new ArrayList<SqlParameter>();
    var relatedDependencies = new ArrayList<RelatedDependency>();

    for (var commentText : extractComments(sql)) {
      for (var line : commentText.split("\\R")) {
        var annotationMatcher = ANNOTATION_LINE.matcher(line);
        if (!annotationMatcher.matches()) {
          continue;
        }

        var key = annotationMatcher.group(1);
        var value = annotationMatcher.group(2).trim();

        switch (key) {
          case "author" -> authors.add(value);
          case "param" -> parseParameter(value).ifPresent(parameters::add);
          case "relatedDependency" ->
              parseRelatedDependency(value).ifPresent(relatedDependencies::add);
          default -> {
            if (SINGLE_VALUED_KEYS.contains(key)) {
              putSingleValued(singleValued, key, value);
            } else {
              log.warn("Unrecognized SQL annotation '@{}'; ignoring it.", key);
            }
          }
        }
      }
    }

    return new ParsedSqlAnnotations(
        singleValued.get("name"),
        singleValued.get("title"),
        singleValued.get("description"),
        singleValued.get("version"),
        singleValued.get("status"),
        singleValued.get("publisher"),
        List.copyOf(authors),
        List.copyOf(parameters),
        List.copyOf(relatedDependencies));
  }

  private void putSingleValued(
      LinkedHashMap<String, String> singleValued, String key, String value) {
    var previous = singleValued.putIfAbsent(key, value);
    if (previous != null) {
      log.warn(
          "Duplicate '@{}' annotation; keeping the first occurrence ('{}') and ignoring '{}'.",
          key,
          previous,
          value);
    }
  }

  /**
   * Finds every block and line comment's inner text, in source order. Block comments are stripped
   * from the SQL before scanning for line comments, so a {@code --} that happens to be inside a
   * {@code /* *&#47;} block isn't also picked up as a (bogus) second comment.
   */
  private List<String> extractComments(String sql) {
    var comments = new ArrayList<String>();

    var blockMatcher = BLOCK_COMMENT.matcher(sql);
    while (blockMatcher.find()) {
      comments.add(blockMatcher.group(1));
    }

    var sqlWithoutBlockComments = BLOCK_COMMENT.matcher(sql).replaceAll("");
    var lineMatcher = LINE_COMMENT.matcher(sqlWithoutBlockComments);
    while (lineMatcher.find()) {
      comments.add(lineMatcher.group(1));
    }

    return comments;
  }

  private Optional<SqlParameter> parseParameter(String value) {
    var matcher = PARAM_VALUE.matcher(value);
    if (!matcher.matches()) {
      log.warn(
          "Malformed '@param' annotation '{}': expected 'name type [description]'; ignoring it.",
          value);
      return Optional.empty();
    }

    var name = matcher.group(1);
    var type = matcher.group(2);
    var description = matcher.group(3);

    if (!ALLOWED_PARAM_TYPES.contains(type)) {
      log.warn(
          "'@param {}' has unsupported type '{}' (expected one of {}); ignoring it.",
          name,
          type,
          ALLOWED_PARAM_TYPES);
      return Optional.empty();
    }

    return Optional.of(new SqlParameter(name, type, description));
  }

  private Optional<RelatedDependency> parseRelatedDependency(String value) {
    var matcher = RELATED_DEPENDENCY_VALUE.matcher(value);
    if (!matcher.matches()) {
      log.warn("Malformed '@relatedDependency' annotation '{}'; ignoring it.", value);
      return Optional.empty();
    }

    var url = matcher.group(1);
    var label = matcher.group(2);

    if (label != null && !VALID_LABEL.matcher(label).matches()) {
      log.warn(
          "'@relatedDependency {}' label '{}' is not a valid SQL identifier; dropping the label"
              + " only, keeping the dependency.",
          url,
          label);
      label = null;
    }

    return Optional.of(new RelatedDependency(url, label));
  }
}
