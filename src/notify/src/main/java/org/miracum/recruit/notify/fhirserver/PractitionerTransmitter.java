package org.miracum.recruit.notify.fhirserver;

import java.util.List;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Practitioner;
import org.miracum.recruit.notify.FhirServerProvider;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;

/** Create list of practitioners in FHIR server if they don't exist yet. */
@Service
public class PractitionerTransmitter {

  private static final Logger LOG = LoggerFactory.getLogger(PractitionerTransmitter.class);

  private final FhirServerProvider fhirClient;

  @Autowired
  public PractitionerTransmitter(FhirServerProvider fhirClient) {
    this.fhirClient = fhirClient;
  }

  public Bundle transmit(List<Practitioner> practitioners) {
    LOG.info("transmitting practitioner list to FHIR server.");

    var bundle = new Bundle();
    bundle.setType(Bundle.BundleType.TRANSACTION);

    //    for (var practitioner : practitioners) {
    //      var contactPoint = PractitionerUtils.getFirstEmailFromPractitioner(practitioner);
    //      if (contactPoint.isPresent()) {
    //        var email = contactPoint.get().getValue();
    //        bundle
    //            .addEntry()
    //            .setFullUrl(practitioner.getIdElement().getValue())
    //            .setResource(practitioner)
    //            .getRequest()
    //            .setUrl("Practitioner")
    //            .setIfNoneExist(String.format("email=%s", email))
    //            .setMethod(Bundle.HTTPVerb.POST);
    //      }
    //    }

    fhirClient.executeSingleConditionalCreate(practitioners);

    return null;

    // return fhirClient.executeTransaction(bundle);
  }
}
