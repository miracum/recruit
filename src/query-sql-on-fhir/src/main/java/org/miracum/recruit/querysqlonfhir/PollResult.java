package org.miracum.recruit.querysqlonfhir;

/** Outcome of a single {@link PollForStudies#pollForStudies()} cycle. */
public record PollResult(int studiesFound, int studiesFailed) {
  public boolean hasFailures() {
    return studiesFailed > 0;
  }
}
