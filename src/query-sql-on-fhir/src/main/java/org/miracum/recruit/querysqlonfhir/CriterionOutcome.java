package org.miracum.recruit.querysqlonfhir;

import org.hl7.fhir.r4.model.Library;

/**
 * A single patient's result for one criterion, already eligibility-facing (i.e. {@code exclude} has
 * been applied) - {@code true} always means "good". {@code null} means the underlying data was
 * missing (unknown), matching the criterion Library's data-absent CASE branch.
 */
public record CriterionOutcome(Library library, String displayText, Boolean met) {}
