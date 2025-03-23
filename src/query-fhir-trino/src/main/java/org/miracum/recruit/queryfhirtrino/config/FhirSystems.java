package org.miracum.recruit.queryfhirtrino.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "fhir.systems")
public record FhirSystems(
    String screeningListIdentifier,
    String screeningListCodeSystem,
    String screeningListStudyReferenceExtension,
    String eligibilityCriteriaTypes) {}
