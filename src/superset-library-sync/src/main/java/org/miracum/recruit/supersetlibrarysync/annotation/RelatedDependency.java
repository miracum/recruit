package org.miracum.recruit.supersetlibrarysync.annotation;

/**
 * A single {@code @relatedDependency: URL [as label]} annotation, corresponding to one {@code
 * Library.relatedArtifact} entry of type {@code depends-on}. {@code label} is {@code null} when
 * omitted, or when present but not a valid SQL identifier (see {@link SqlAnnotationParser}).
 */
public record RelatedDependency(String url, String label) {}
