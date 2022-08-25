package org.miracum.recruit.query.config;

import javax.sql.DataSource;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.jdbc.repository.config.AbstractJdbcConfiguration;
import org.springframework.jdbc.datasource.DriverManagerDataSource;

@Configuration
public class DataSourceConfig extends AbstractJdbcConfiguration {
  @Bean
  public static DataSource dataSource(
      @Value("${omop.jdbcUrl}") String jdbcUrl,
      @Value("${omop.username}") String username,
      @Value("${omop.password}") String password,
      @Value("${omop.cdmSchema}") String cdmSchema) {
    var ds = new DriverManagerDataSource(jdbcUrl);
    ds.setDriverClassName("org.postgresql.Driver");
    ds.setUrl(jdbcUrl);
    ds.setUsername(username);
    ds.setPassword(password);
    // use the cdm schema as the default schema
    ds.setSchema(cdmSchema);
    return ds;
  }
}
