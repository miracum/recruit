package org.miracum.recruit.supersetlibrarysync;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;

@ConfigurationProperties(prefix = "sync")
public record SyncProperties(
    // when true, Library resources are built and logged but not submitted to the FHIR server -
    // lets a run be previewed before it's allowed to actually change server state.
    @DefaultValue("false") boolean dryRun) {}
