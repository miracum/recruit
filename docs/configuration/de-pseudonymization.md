# De-Pseudonymization

!!! note ""

    Requires at least version 2.1.0 of the [MIRACUM FHIR Pseudonymizer](https://github.com/miracum/fhir-pseudonymizer)
    to be deployed.

If the `Patient` and `Encounter` resources in the FHIR server are pseudonymized (which is true if the OMOP DB is pseudonymized),
you may want to de-pseudonymize them before displaying them in the screening list in order to show the original patient
and encounter identifiers.

On the `list` module, you need to set the environment variable `DE_PSEUDONYMIZATION_ENABLED` to `true`. This will cause
all resources fetched from the FHIR server to be sent to the `$de-pseudonymize` endpoint of the
[FHIR Pseudonymizer](https://github.com/miracum/fhir-pseudonymizer) first, which reverts any pseudonymization or encryption
previously applied to the resources. `DE_PSEUDONYMIZATION_SERVICE_URL` must be set to the base URL of this service.
Because this endpoint is protected by a basic API key, you will need to configure the same key in the pseudonymizer's
`APIKEY` and the list module's `DE_PSEUDONYMIZATION_API_KEY`.
