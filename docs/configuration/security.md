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

## Configuring the Query Module to access a secured WebAPI Instance

If the [OHDSI WebAPI requires authentication](https://github.com/OHDSI/WebAPI/wiki/Security-Configuration),
you need to configure the query module accordingly. The relevant environment variables to set start with
`QUERY_WEBAPI_AUTH_` (see [the configuration overview](options.md)).

Be sure to give the created user relevant roles to access the OMOP-CDMV5 source, access cohort definitions,
and generate cohorts.

You can also combine multiple authentication methods, for example use OpenID to allow users to login via the
Atlas UI but create a dedicated service account for the query module which uses WebAPI basic security.

## Verify container image integrity

All released images are signed via [cosign](https://github.com/sigstore/cosign). To verify the integrity of the images, run:

<!-- x-release-please-start-version -->
```sh
cosign verify -key recruit-image-signing.pub ghcr.io/miracum/recruit/list:v2.10.1
```
<!-- x-release-please-end -->

where `recruit-image-signing.pub` is located in the root of the main repository.

Tools such as [connaisseur](https://github.com/sse-secure-systems/connaisseur) allow you to automatically verify these
signatures when deploying to Kubernetes.
