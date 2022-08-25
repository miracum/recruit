package org.miracum.recruit.query;

import com.google.common.collect.Sets;
import java.util.Set;
import org.apache.commons.lang3.StringUtils;
import org.springframework.stereotype.Component;

@Component
public class LabelExtractor {

  public Set<String> extract(String stringWithLabels) {
    if (StringUtils.isBlank(stringWithLabels)) {
      return Set.of();
    }
    return extractAll(stringWithLabels);
  }

  public Set<String> extractAll(String stringWithLabels) {
    var substrings = StringUtils.substringsBetween(stringWithLabels, "[", "]");
    if (substrings == null) {
      return Set.of();
    }
    return Sets.newHashSet(substrings);
  }

  public boolean hasLabel(String stringWithLabels, String searchedForLabel) {
    return extractAll(stringWithLabels).contains(searchedForLabel);
  }

  /**
   * Checks if Set of Labels contains a specific tag with format [tag=xxx]
   *
   * @param stringWithLabels set of all labels
   * @return extracted tag
   */
  public String extractByTag(String tag, String stringWithLabels) {
    Set<String> labels = extractAll(stringWithLabels);
    String value = "";

    // TODO: could be replaced with a RegEx
    for (String label : labels) {
      if (label.contains(tag)) {
        var splitted = label.split("[=:]");
        if (splitted.length > 1) {
          value = splitted[1];
        }
      }
    }
    if (value.isBlank()) {
      return null;
    }
    value = value.trim();
    return value;
  }
}
