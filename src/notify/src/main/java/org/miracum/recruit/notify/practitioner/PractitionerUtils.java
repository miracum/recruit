package org.miracum.recruit.notify.practitioner;

import java.util.Optional;
import org.hl7.fhir.r4.model.ContactPoint;
import org.hl7.fhir.r4.model.ContactPoint.ContactPointSystem;
import org.hl7.fhir.r4.model.Practitioner;

public final class PractitionerUtils {
  private PractitionerUtils() {}

  public static Optional<ContactPoint> getFirstEmailFromPractitioner(Practitioner practitioner) {
    return practitioner.getTelecom().stream()
        .filter(com -> com.getSystem().equals(ContactPointSystem.EMAIL))
        .findFirst();
  }

  public static boolean hasEmail(Practitioner practitioner, String email) {
    return practitioner.getTelecom().stream()
        .anyMatch(
            com ->
                com.getSystem().equals(ContactPointSystem.EMAIL) && com.getValue().equals(email));
  }
}
