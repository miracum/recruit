package org.miracum.recruit.notify.practitioner;

import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;
import org.hl7.fhir.r4.model.ContactPoint;
import org.hl7.fhir.r4.model.ContactPoint.ContactPointSystem;
import org.hl7.fhir.r4.model.ContactPoint.ContactPointUse;
import org.hl7.fhir.r4.model.Identifier;
import org.hl7.fhir.r4.model.Practitioner;
import org.miracum.recruit.notify.fhirserver.FhirSystemsConfig;
import org.miracum.recruit.notify.mailconfig.UserConfig;
import org.miracum.recruit.notify.mailconfig.UserConfig.Subscription;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;

/** Create a list of FHIR Practitioners from the users config. */
@Service
public class PractitionerCreator {
  private static final Logger LOG = LoggerFactory.getLogger(PractitionerCreator.class);

  private final FhirSystemsConfig fhirSystems;
  private final UserConfig users;

  @Autowired
  public PractitionerCreator(FhirSystemsConfig fhirSystems, UserConfig users) {
    this.fhirSystems = fhirSystems;
    this.users = users;
  }

  public List<Practitioner> create() {
    LOG.info(
        "creating list of practitioners to be referenced by the CommunicationRequest resources");
    var emailSet = extractEmailAddressesFromSubscriptions();

    return emailSet.stream()
        .map(this::createPractitionerResourceFromEmail)
        .collect(Collectors.toList());
  }

  private Set<String> extractEmailAddressesFromSubscriptions() {
    if (users.getTrials() == null) {
      LOG.warn("No trials specified in configuration");
      return Set.of();
    }

    return users.getTrials().stream()
        .flatMap(t -> t.getSubscriptions().stream())
        .map(Subscription::getEmail)
        .collect(Collectors.toSet());
  }

  private Practitioner createPractitionerResourceFromEmail(String email) {
    var emailContactPoint =
        new ContactPoint()
            .setSystem(ContactPointSystem.EMAIL)
            .setValue(email)
            .setUse(ContactPointUse.WORK);
    var identifier = new Identifier().setSystem(fhirSystems.getSubscriberId()).setValue(email);

    return new Practitioner()
        .setActive(true)
        .addIdentifier(identifier)
        .setTelecom(List.of(emailContactPoint));
  }
}
