package org.miracum.recruit.supersetlibrarysync.superset;

import java.net.URI;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;

@ConfigurationProperties(prefix = "superset")
public record SupersetProperties(
    URI url,
    String username,
    String password,
    // only saved queries carrying this tag are synced; null/blank syncs every saved query that
    // has at least one recognized SQL annotation (see ParsedSqlAnnotations.isEmpty).
    String tagFilter,
    @DefaultValue("trino") String sqlDialect,
    @DefaultValue("100") int pageSize,
    @DefaultValue("60") long timeoutInSeconds) {}
