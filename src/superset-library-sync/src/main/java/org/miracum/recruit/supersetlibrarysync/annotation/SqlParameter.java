package org.miracum.recruit.supersetlibrarysync.annotation;

/**
 * A single {@code @param: name type [description]} annotation, corresponding to one {@code
 * Library.parameter} entry. {@code description} is {@code null} when omitted.
 */
public record SqlParameter(String name, String type, String description) {}
