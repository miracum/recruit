package org.miracum.recruit.query;

import java.util.List;
import lombok.AllArgsConstructor;
import lombok.Getter;
import org.hl7.fhir.r4.model.ListResource;
import org.hl7.fhir.r4.model.Patient;
import org.hl7.fhir.r4.model.ResearchSubject;

@AllArgsConstructor
@Getter
public class ScreeningListResources {

  private ListResource screeningList;
  private List<Patient> patients;
  private List<ResearchSubject> researchSubjects;
}
