# Configuration Options

This is a non-exhaustive list of available configuration options for the recruIT deployment.

!!! note ""

    When deploying using Helm, many of these options are automatically configured or exposed via `values.yaml`.
    You can find a complete description of all available chart configuration options here:
    <https://github.com/miracum/charts/blob/master/charts/recruit/README.md#configuration>. If anything is missing, you
    can use the `extraEnv` option to supply additional environment variables to the modules.

"Staging Default Value" refers to the default value for that option when [deploying via Docker Compose](../deployment/docker-compose.md)
using the `.staging.env` file.

## Used by multiple modules

| Variable | Description                                                                                                           | Staging Default Value   |
| -------- | --------------------------------------------------------------------------------------------------------------------- | ----------------------- |
| FHIR_URL | URL of a FHIR server used to store the screening lists and retrieve patient data (if available). Used by all modules. | `http://fhir:8080/fhir` |

## Screening List

| Variable                        | Description                                                                                                                                                                                | Staging Default Value                    |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------- |
| KEYCLOAK_DISABLED               | Disable Keycloak authentication for the screening list.                                                                                                                                    | `false`                                  |
| KEYCLOAK_CLIENT_ID              | Keycloak client id for the screening list component.                                                                                                                                       | `uc1-screeninglist`                      |
| KEYCLOAK_REALM                  | The Keycloak realm.                                                                                                                                                                        | `MIRACUM`                                |
| KEYCLOAK_AUTH_URL               | URL of the Keycloak server (should end with `/auth`).                                                                                                                                      | `http://host.docker.internal:38086/auth` |
| DE_PSEUDONYMIZATION_ENABLED     | Whether or not the resources from the FHIR server should be de-pseudonymized before being displayed in the screening list. See [De-Pseudonymization for details](./de-pseudonymization.md) | `false`                                  |
| DE_PSEUDONYMIZATION_SERVICE_URL | The URL to the [FHIR Pseudonymizer](https://gitlab.miracum.org/miracum/etl/fhir-pseudonymizer) service used for de-pseudonymization.                                                       | `""`                                     |
| DE_PSEUDONYMIZATION_API_KEY     | The API key used to authenticate against the FHIR Pseudonymizer                                                                                                                            | `""`                                     |
| HIDE_DEMOGRAPHICS               | Don't show age and gender of the persons                                                                                                                                                   | `false`                                  |
| HIDE_LAST_VISIT                 | Don't show the last visit information                                                                                                                                                      | `false`                                  |
| HIDE_EHR_BUTTON                 | Don't show the button to show EHR information of the person                                                                                                                                | `false`                                  |
| PROXY_IS_SECURE_BACKEND         | If `FHIR_URL` points to a server using HTTPS, then you should set this to `1` or `true`                                                                                                    | `false`                                  |

## Query Module

| Variable                                      | Description                                                                                                                                                                                                     | Staging Default Value                 |
| --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------- |
| ATLAS_URL                                     | URL of the ATLAS WebAPI endpoint. Usually ends in `/WebAPI`. Used by the query module.¹                                                                                                                         | `http://ohdsi-webapi:8080/WebAPI`     |
| ATLAS_DATASOURCE                              | Name of the ATLAS datasource used to generate the cohorts from.                                                                                                                                                 | `OHDSI-CDMV5`                         |
| OMOP_JDBCURL                                  | JDBC URL of the OMOP database.                                                                                                                                                                                  | `jdbc:postgresql://omopdb:5432/OHDSI` |
| OMOP_USERNAME                                 | Username to access the OMOP database.                                                                                                                                                                           | `postgres`                            |
| OMOP_PASSWORD                                 | Password to access the OMOP database.                                                                                                                                                                           | `postgres`                            |
| OMOP_RESULTSSCHEMA                            | Name of the database schema containing the results of the cohort generation.                                                                                                                                    | `synpuf_results`                      |
| OMOP_CDMSCHEMA                                | Name of the database schema containing the actual clinical data.                                                                                                                                                | `synpuf_cdm`                          |
| QUERY_SCHEDULE_UNIXCRON                       | A UNIX-compliant CRON expression to configure the execution schedule of the query module (see <https://crontab.guru/>).                                                                                         | `*/5 * * * *` (Run every 5 minutes)   |
| QUERY_SELECTOR_MATCHLABELS                    | Comma-separated list of labels which must be present in either the cohort's name or description enclosed in `[]` in order to be processed by the query module.²                                                 | `UC1,Test`                            |
| QUERY_WEBAPI_AUTH_ENABLED                     | Set to true if the OHDSI WebAPI requires authentication.                                                                                                                                                        | `false`                               |
| QUERY_WEBAPI_AUTH_LOGIN_PATH                  | The login method to use. See <https://github.com/OHDSI/Atlas/blob/master/js/config/app.js#L20> for a list of possible paths.                                                                                    | `/user/login/db`                      |
| QUERY_WEBAPI_AUTH_USERNAME                    | The username to login as. Note that this user needs permissions to query and generate cohorts.                                                                                                                  | `""`                                  |
| QUERY_WEBAPI_AUTH_PASSWORD                    | The password used to login.                                                                                                                                                                                     | `""`                                  |
| QUERY_APPEND_RECOMMENDATIONS_TO_EXISTING_LIST | if true, instead of overwriting the contents of the List for each cohort based on what the last generation run returned, append to this list                                                                    | `false`                               |
| QUERY_FORCE_UPDATE_SCREENING_LIST             | if true, always send a List resource as part of the transaction even if nothing changed                                                                                                                         | `false`                               |
| QUERY_ONLY_CREATE_PATIENTS_IF_NOT_EXIST       | if true, send Patient resources as "conditional-creates" on their first identifier instead of using "conditional-update". Useful if the server is already filled with Patient resources from a different system | `false`                               |
| QUERY_COHORTSIZETHRESHOLD                     | Maximum number of patients to be included in the generated screening list. A warning will appear on the screening list view if a list exceeded this count.                                                      | `100`                                 |
| QUERY_WEBAPI_COHORT_CACHE_SCHEMA              | The name of the schema which contains the cohort definitions cache.                                                                                                                                             | `ohdsi`                               |

¹: This is usually the same URL you configured in the `config-local.js` file when setting up the ATLAS server.

²: For example, the default values of `UC1,Test` require the cohort definitions name or description in Atlas to contain either
the string `[UC1]` or `[Test]` in order to be processed by the query module.

## Notification Module

| Variable                   | Description                                                                                                                                  | Staging Default Value                                            |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| NOTIFY_WEBHOOK_ENDPOINT    | External URL for the notification module's webhook endpoint. Should end in `/on-list-change`.³                                               | `http://notify:8080/on-list-change`                              |
| NOTIFY_RULES_CONFIG_PATH   | Path to the notification rule configuration file. The file is mounted inside the notification module.                                        | `./staging/notify-rules.yml`                                     |
| NOTIFY_MAIL_HOST           | Host of the SMTP server used to send notification emails.                                                                                    | `maildev`                                                        |
| NOTIFY_MAIL_SMTP_PORT      | SMTP port on the host.                                                                                                                       | `1025`                                                           |
| NOTIFY_MAIL_USERNAME       | SMTP username.                                                                                                                               | `user`                                                           |
| NOTIFY_MAIL_PASSWORD       | SMTP password.                                                                                                                               | `pass`                                                           |
| NOTIFY_MAILER_LINKTEMPLATE | Template used to generate a clickable link in the notification emails. `[list_id]` is mandatory and is replaced with the lists internal id.⁴ | `http://recruit-list.127.0.0.1.nip.io/recommendations/[list_id]` |
| NOTIFY_MAILER_FROM         | The sender email address for the created notification mails.                                                                                 | `rekrutierung@miracum.org`                                       |

³: If your FHIR server is running on `fhir.example.com` and your notification module runs on `notify.example.com:8080`,
then this value should be set to `http://notify.example.com:8080/on-list-change`. The default exposed port of the notification module is `38087`. This external port is used when the FHIR server can't access the notification module using the Docker-network internal host and port `notify:8080`.

⁴: If your screening list is running on `https://list.example.com:38080/`, then this value should be set to
`https://list.example.com:38080/recommendations/[list_id]`.
