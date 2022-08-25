package org.miracum.recruit.notify.fhirserver;

import static net.logstash.logback.argument.StructuredArguments.kv;

import ca.uhn.fhir.rest.client.api.IGenericClient;
import org.hl7.fhir.r4.model.CommunicationRequest.CommunicationRequestStatus;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;

/** Update CommunicationRequest resources as completed when they were sent successfully. */
@Service
public class MessageStatusUpdater {

  private static final Logger LOG = LoggerFactory.getLogger(MessageStatusUpdater.class);

  private final IGenericClient fhirClient;

  @Autowired
  public MessageStatusUpdater(IGenericClient fhirClient) {
    this.fhirClient = fhirClient;
  }

  public void update(String relativeId, CommunicationRequestStatus status) {
    // use once the HAPI FHIR server supports FHIRPath patches:
    //    var patch = new Parameters();
    //    var operation = patch.addParameter();
    //    operation.setName("operation");
    //    operation.addPart().setName("type").setValue(new CodeType("replace"));
    //    operation.addPart().setName("path").setValue(new
    //      StringType("CommunicationRequest.status"));
    //    operation.addPart().setName("value").setValue(new StringType(status.toCode()));

    var jsonPatch =
        String.format(
            "[{\"op\": \"replace\", \"path\": \"/status\", \"value\": \"%s\"}]", status.toCode());

    // Invoke the patch
    var outcome =
        fhirClient
            .patch()
            .withBody(jsonPatch)
            .withId("CommunicationRequest/" + relativeId)
            .execute();

    LOG.info(
        "updated {} status to {}",
        kv("message", outcome.getId().getIdPart()),
        kv("status", status));
  }
}
