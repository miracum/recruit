package org.miracum.recruit.query;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.jdbc.repository.config.EnableJdbcRepositories;

@SpringBootApplication
@Configuration
@EnableJdbcRepositories
public class QueryApplication {
  public static void main(String[] args) {
    SpringApplication.run(QueryApplication.class, args);
  }
}
