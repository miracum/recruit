package org.miracum.recruit.query;

import lombok.Data;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationProperties(prefix = "fhir.systems")
@Data
public class FhirSystems {
  private String omopSubjectIdentifier;
  private String omopCohortIdentifier;
  private String screeningListIdentifier;
  private String screeningListStudyReferenceExtension;
  private String researchStudyAcronym;
  private String screeningListCoding;
  private String studySource;
  private String patientId;
  private String identifierType;
  private String encounterId;
  private String subEncounterId;
  private String actEncounterCode;
  private String listOrder;
  private String deviceId;
  private String systemDeterminedSubjectStatus;
}
