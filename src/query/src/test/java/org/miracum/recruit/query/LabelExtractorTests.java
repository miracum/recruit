package org.miracum.recruit.query;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.junit.jupiter.params.provider.ValueSource;

class LabelExtractorTests {

  private final LabelExtractor sut = new LabelExtractor();

  @Test
  void extract_WithDuplicateLabels_returnsDisctinct() {
    var result = sut.extract("[a] abc [b] [a]");

    assertThat(result).contains("a", "b");
  }

  @ParameterizedTest
  @ValueSource(strings = {"", "hello world ]"})
  void extract_withGivenString_returnsEmptySet() {
    var result = sut.extract("");

    assertThat(result).isEmpty();
  }

  @Test
  void extract_withNullString_returnsEmptySet() {
    var result = sut.extract(null);

    assertThat(result).isEmpty();
  }

  @ParameterizedTest
  @CsvSource({
    "This is a description with one acronym [acronym=Test] and label [Testlabel], acronym, Test",
    "This is a description without a tag but with [label], acronym, ",
    "[acronym=Test], acronym, Test",
    "[acronym=Test] [hello=World], acronym, Test"
  })
  void extractTag_longTextWithOneTag_returnsOneTag(
      String text, String searchedForLabel, String expectedResult) {
    var result = sut.extractByTag(searchedForLabel, text);
    assertThat(result).isEqualTo(expectedResult);
  }

  @Test
  void extractTag_withColonInsteadOfEqualSign_shouldAlsoMatch() {
    var result = sut.extractByTag("acronym", "[acronym:Test]");
    assertThat(result).isEqualTo("Test");
  }

  @ParameterizedTest
  @CsvSource({
    "[test], test, true",
    "A longer text [] with some more words [test=false], test, false",
    "A [acronym=Test] [hello=World], longer text [] with some more words [test], test, true",
  })
  void hasLabel_withGivenString_shouldReturnTrueIfStringContainsLabel(
      String text, String searchedForLabel, boolean expectedResult) {
    var result = sut.hasLabel(text, searchedForLabel);
    assertThat(result).isEqualTo(expectedResult);
  }
}
