package org.miracum.recruit.notify.practitioner;

import java.util.List;
import lombok.Data;
import org.hl7.fhir.r4.model.Practitioner;

/** Data structure to store different kinds of fhir practitioner items as separate lists. */
@Data
public class PractitionerListContainer {
  private List<Practitioner> adHocRecipients;
  private List<Practitioner> scheduledRecipients;
}
