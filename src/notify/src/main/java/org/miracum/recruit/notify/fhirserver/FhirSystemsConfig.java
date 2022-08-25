package org.miracum.recruit.notify.fhirserver;

import lombok.Data;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationProperties(prefix = "fhir.systems")
@Data
public class FhirSystemsConfig {
  private String screeningListReference;
  private String studyAcronym;
  private String subscriberId;
  private String communication;
  private String communicationStatusReason;
  private String communicationCategory;
}
