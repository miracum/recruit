# Security

## Authentication and Authorization for the Screening List

Access to the screening list can configured using Keycloak as an identity provider.
Since the screening list is a Single-Page Application, no client secret needs to be provided.
Instead, make sure the screening list is only accessible behind TLS and the redirect URLs in Keycloak are set accordingly.

To limit the screening lists and screening recommendations individual users can see, the notification rule configuration
is used.
By default, every user with a configured notification subscription is allowed to access the corresponding screening list.
This ensures that when one receives an email about new screening recommendations, they can access these recommendations
without extra configuration. Further, a new `accessibleBy.users` has been added to the trials. This allows specifying
additional users either by email or by username which can access the trial's screening recommendations.

```yaml
notify:
  rules:
    schedules:
      everyMorning: "0 0 8 1/1 * ? *"
      everyMonday: "0 0 8 ? * MON *"
      everyHour: "0 0 0/1 1/1 * ? *"
      everyFiveMinutes: "0 0/5 * 1/1 * ? *"

    # trials are identified by their acronym which corresponds to the cohort's title in Atlas or the "[acronym=XYZ]" tag
    trials:
      # By default, every user under 'subscriptions' is also allowed to access the corresponding screening list,
      # in the special case of '*', the user with the everything@example.com address is allowed to access every
      # list.
      - acronym: "*"
        subscriptions:
          - email: "everything@example.com"

      - acronym: "SAMPLE"
        # the new "accessibleBy" key allows specifying users either by username or email address that
        # are allowed to access the screening list
        accessibleBy:
          users:
            - "user1"
            - "user.two@example.com"
        subscriptions:
          - email: "everyMorning@example.com"

      - acronym: "AMICA"
        subscriptions:
          - email: "everyHour1@example.com"
            notify: "everyHour"
```

Any user with the `admin` role inside the screening list client in Keycloak is allowed to access all recommendations:

![Keycloak configuration for an admin user](../_img/security/keycloak-admin-role.png)

You can disable authorization by not mounting the `notify-rules.yaml` inside the container; if no config is found,
then no permissions are checked.

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
IMAGE=ghcr.io/miracum/recruit/list:v10.1.7
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
