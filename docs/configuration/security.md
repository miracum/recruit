# Security

## Authentication and Authorization for the Screening List

Access to the screening list can be configured using Keycloak as an identity provider. Because the `list` module is
a Blazor Server app (server-rendered, not a Single-Page Application), the OIDC login is handled entirely server-side:
configure it either as a public client (authorization code flow with PKCE, the default, no client secret required) or
as a confidential client by additionally setting `KEYCLOAK_CLIENT_SECRET`. Either way, make sure the screening list
is only accessible behind TLS and the redirect URLs in Keycloak are set accordingly.

!!! warning "No per-study access control"

    As of `list` module version 11 (the Blazor Server rewrite), access control is all-or-nothing: any user who can
    authenticate against the configured Keycloak realm can see and edit candidates for **every** study. The
    previous per-study, subscription- and role-based filtering described below (via the notification rule
    configuration and the `admin` client role) applied only to the old Node.js/Vue implementation and has not been
    reimplemented. If you need to restrict individual users to specific studies, either front the module with your
    own authorization proxy, or keep users who shouldn't see all studies out of the Keycloak realm/client entirely
    until per-study access control is reimplemented.

## Configuring the Query Module to access a secured WebAPI instance

If the [OHDSI WebAPI requires authentication](https://github.com/OHDSI/WebAPI/wiki/Security-Configuration),
you need to configure the query module accordingly. The relevant environment variables to set start with
`QUERY_WEBAPI_AUTH_` (see [the configuration overview](options.md)).

Be sure to give the created user relevant roles to access the OMOP-CDMV5 source, access cohort definitions,
and generate cohorts.

You can also combine multiple authentication methods, for example use OpenID to allow users to login via the
Atlas UI but create a dedicated service account for the query module which uses WebAPI basic security.

## Verify container image signatures and SLSA provenance

Prerequisites:

- [cosign](https://github.com/sigstore/cosign/releases)
- [slsa-verifier](https://github.com/slsa-framework/slsa-verifier/releases)
- [crane](https://github.com/google/go-containerregistry/releases)

All released container images are signed using [cosign](https://github.com/sigstore/cosign) and SLSA Level 3 provenance
is available for verification.

<!-- x-release-please-start-version -->

```sh
# for example, verify the `list` module's container image. Same workflow applies to `query` and `notify`.
IMAGE=ghcr.io/miracum/recruit/list:v10.5.7
DIGEST=$(crane digest "${IMAGE}")
IMAGE_DIGEST_PINNED="ghcr.io/miracum/recruit/list@${DIGEST}"
IMAGE_TAG="${IMAGE#*:}"

cosign verify \
   --certificate-oidc-issuer=https://token.actions.githubusercontent.com \
   --certificate-identity="https://github.com/miracum/recruit/.github/workflows/build.yaml@refs/tags/${IMAGE_TAG}" \
   "${IMAGE_DIGEST_PINNED}"

slsa-verifier verify-image \
    --source-uri github.com/miracum/recruit \
    --source-tag ${IMAGE_TAG} \
    "${IMAGE_DIGEST_PINNED}"
```

<!-- x-release-please-end -->

See also <https://github.com/slsa-framework/slsa-github-generator/tree/main/internal/builders/container#verification>
for details on verifying the image integrity using automated policy controllers.
