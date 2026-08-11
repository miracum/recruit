package org.miracum.recruit.querysqlonfhir.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "fhir.systems")
public record FhirSystems(
    String screeningListIdentifier,
    String screeningListCodeSystem,
    String screeningListStudyReferenceExtension,
    String eligibilityObservationCategorySystem) {}
