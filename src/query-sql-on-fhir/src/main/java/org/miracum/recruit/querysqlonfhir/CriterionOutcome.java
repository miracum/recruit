package org.miracum.recruit.querysqlonfhir;

import org.hl7.fhir.r4.model.Library;

/**
 * A single patient's result for one criterion, already eligibility-facing (i.e. {@code exclude} has
 * been applied) - {@code true} always means "good". {@code null} means the result couldn't be
 * determined; {@code indeterminate} distinguishes *why* within that {@code null} case - {@code
 * false} means the underlying data was simply missing (unknown), {@code true} means the criterion's
 * SQL was actually evaluated but reached an inconclusive result. Both behave identically in the
 * Kleene merge (see {@link PatientEligibilityResult#overallMet()}) - the distinction only matters
 * for how the result is displayed. {@code note} is an optional, free-text explanation from the
 * criterion's SQL (its {@code result_note} column) - most useful alongside {@code indeterminate},
 * but not limited to it.
 */
public record CriterionOutcome(
    Library library, String displayText, Boolean met, boolean indeterminate, String note) {}
