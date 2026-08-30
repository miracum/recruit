package org.miracum.recruit.supersetlibrarysync.superset;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;

@ConfigurationProperties(prefix = "superset")
public record SupersetProperties(@DefaultValue("trino") String sqlDialect) {}
