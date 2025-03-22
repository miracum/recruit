package org.miracum.recruit.queryfhirtrino.config;

import javax.sql.DataSource;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration(proxyBeanMethods = false)
public class DataSourceConfig {
  @Bean
  @ConfigurationProperties(prefix = "trino.datasource")
  public DataSource dataSource() {
    return DataSourceBuilder.create().build();
  }
}
