package org.miracum.recruit.querysqlonfhir;

import java.util.List;

/**
 * One patient's merged eligibility outcome across every criterion of a study. Only patients whose
 * {@link #overallMet()} is not definitely {@code false} (i.e. candidate or unknown) should ever be
 * constructed here - definite non-matches are filtered out at the source (in SQL for the Trino
 * path, in Java for the delegated fallback) rather than being carried around and dropped later.
 */
public record PatientEligibilityResult(String patientId, List<CriterionOutcome> criteria) {

  /**
   * Three-valued (Kleene) AND across all criteria: {@code true} only if every criterion is met,
   * {@code false} if any criterion is definitely not met (regardless of unknowns elsewhere), and
   * {@code null} (unknown) only when nothing is definitely not met but at least one criterion's
   * data is missing.
   */
  public Boolean overallMet() {
    Boolean result = Boolean.TRUE;
    for (var criterion : criteria) {
      result = kleeneAnd(result, criterion.met());
    }
    return result;
  }

  private static Boolean kleeneAnd(Boolean a, Boolean b) {
    if (Boolean.FALSE.equals(a) || Boolean.FALSE.equals(b)) {
      return Boolean.FALSE;
    }
    if (a == null || b == null) {
      return null;
    }
    return a && b;
  }
}
