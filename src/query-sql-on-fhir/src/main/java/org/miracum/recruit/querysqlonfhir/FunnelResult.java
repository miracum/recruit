package org.miracum.recruit.querysqlonfhir;

import java.util.List;

/**
 * A study's eligibility attrition funnel: {@code totalPopulation} - an independent count, not
 * derived from any criterion's own SQL (see {@link SqlQueryExecutor#computeFunnel}), since a
 * criterion's SQL (especially the anchor, {@code crit_0}) may already apply a narrowing {@code
 * WHERE} clause and so isn't reliable as "everyone who was screened" - followed by one {@link Step}
 * per criterion, in {@code Group.characteristic} order, holding how many patients remain a
 * candidate or unknown after cumulatively applying every criterion up to and including that one.
 * {@code steps.getLast().remainingCount()} is expected to equal the same study's final
 * candidate/unknown count from {@link SqlQueryExecutor#evaluateEligibility} - both apply identical
 * Kleene-AND/{@code IS DISTINCT FROM FALSE} semantics, just one incrementally and one only at the
 * end.
 */
public record FunnelResult(long totalPopulation, List<Step> steps) {

  /** One criterion's cumulative funnel step - see {@link FunnelResult}. */
  public record Step(EligibilityCriterion criterion, long remainingCount) {}
}
