package org.miracum.recruit.querysqlonfhir;

import org.hl7.fhir.r4.model.Library;

/**
 * One criterion from a study's eligibility Group.characteristic: a reference to the Library
 * containing its SQL, its human-readable description (Group.characteristic.code.text), and whether
 * meeting the raw predicate excludes (true) or includes (false) the patient.
 */
public record EligibilityCriterion(Library library, String displayText, boolean exclude) {}
