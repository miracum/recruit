package org.miracum.recruit.query.models;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import lombok.Data;

@Data
@JsonIgnoreProperties(ignoreUnknown = true)
public class CohortDefinition {

  private Long id;
  private String name;
  private String description;
}
