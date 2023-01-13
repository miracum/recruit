package org.miracum.recruit.query;

import java.util.HashSet;
import java.util.Set;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationProperties(prefix = "query.selector")
public class CohortSelectorConfig {

  private final Set<String> list;

  public CohortSelectorConfig() {
    this.list = new HashSet<>();
  }

  public Set<String> getMatchLabels() {
    return this.list;
  }
}
