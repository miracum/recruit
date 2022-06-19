# Using an existing FHIR server

## FHIR subscription support

The notification module relies on the server's [FHIR Subscription](https://www.hl7.org/fhir/subscription.html) support
to be notified whenever a screening list has been updated by the query module. Many FHIR servers don't support
this FHIR subscription mechanism, but there is no technical reason for the recruIT infrastructure to entirely depend on it.
Please create an issue if support for a non-HAPI FHIR server is desired.

## Pre-filled server

By default, the recruIT tool only needs clinical data from OMOP and Atlas and automatically creates FHIR Patient,
Encounter, ResearchStudy, and ResearchSubject resources in the FHIR server to populate the screening list.

If you have an existing FHIR server already filled with at least Patient and Encounter resources, you can use it to display
additional information in the last known stay column of the screening list (contact details and a more detailed description
of the last known stay location if the Encounter provides it).

Further, if the existing server contains Condition, Procedure, Observation, and MedicationStatement resources, a small
integrated patient record is populated.

To make sure the existing patient resources are correctly referenced within the screening list, you have to configure the
patient identifier FHIR system in the query module. Add the following environment variable to the `query` service:

`FHIR_SYSTEMS_PATIENT_ID: https://fhir.miracum.org/core/NamingSystem/patientId`

replacing `https://fhir.miracum.org/core/NamingSystem/patientId` with the MRN identifier used by the Patient resources
already existing in the server.

You can prevent the query module from creating Encounter resources itself by setting:

`QUERY_EXCLUDE_PATIENT_PARAMETERS_ENCOUNTER: true`

and prevent it from overriding existing Patient resources by setting:

`QUERY_ONLY_CREATE_PATIENTS_IF_NOT_EXIST: true`

!!! danger "Race condition on concurrent writes to the FHIR server"

    There is potential for a race condition on the Patient resources created by the query module and the existing
    ones if the latter are continuously and possibly asynchronously transmitted to the server: the query module maps
    OMOP's `person` table to Patient resources and sends them to the FHIR server as conditional updates on the patient
    identifier. If a Patient resource with the same identifier already exists, its contents are simply overwritten.

    If the query module is able to generate a Patient with an identifier that doesn't yet exist in the FHIR server and
    the other mechanism to send existing resources to the server is using "upsert" or "update-as-create" semantics to
    send a Patient after the fact, then two Patient resources with the same identifier may exist in the server. This will
    cause a conflict the next time the query module tries to update the Patient and can only be resolved by deleting the
    resource first created by the query module.

    A solution would be to ensure that all Patient resources exist in the FHIR
    server before the query module is ran. Or the existing mechanism to send resources should be switched to using
    conditional-updates/conditional-creates.
