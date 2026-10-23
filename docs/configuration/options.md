# Configuration Options

The recruIT modules are configured through environment variables. This page lists every setting of the screening list
(`list`) and the query module (`query-sql-on-fhir`).

How you set them depends on how recruIT is deployed:

- **Docker Compose**: `compose.yaml` passes the most important settings from variables in your `.env` file, see
  [below](#docker-compose). To set any other setting on this page, add it to the service's `environment` in a
  `compose.override.yaml`, see [Mail server and port](../deployment/compose.md#mail-server-and-port) for an example.
- **Helm**: the chart sets these from its values, which are documented in the
  [chart's README](https://github.com/open-recruit/recruit/blob/master/charts/recruit/README.md#values). For a setting
  the chart doesn't expose, use `list.extraEnv` or `querySqlOnFhir.extraEnv`.

## Docker Compose

These are the variables `compose.yaml` reads from your `.env` file, and the module settings they set:

| `.env` variable                   | Required | Screening list setting      | Query module setting              |
| --------------------------------- | -------- | --------------------------- | --------------------------------- |
| `FHIR_SERVER_BASE_URL`            | yes      | `FhirServer__BaseUrl`       | `FHIR_SERVER_BASE_URL`            |
| `LIST_CONNECTIONSTRINGS_APPDB`    | yes      | `ConnectionStrings__AppDb`  |                                   |
| `PGPASSWORD`                      | yes      | `PGPASSWORD`                |                                   |
| `AUTH_ENABLED`                    | no       | `Auth__Enabled`             |                                   |
| `AUTH_OIDC_AUTHORITY`             | yes¹     | `Auth__Oidc__Authority`     |                                   |
| `AUTH_OIDC_CLIENT_ID`             | yes¹     | `Auth__Oidc__ClientId`      |                                   |
| `AUTH_OIDC_CLIENT_SECRET`         | yes¹     | `Auth__Oidc__ClientSecret`  |                                   |
| `AUTH_OIDC_ROLE_CLAIM_TYPE`       | no       | `Auth__Oidc__RoleClaimType` |                                   |
| `TRINO_JDBC_URL`                  | yes      |                             | `SPRING_DATASOURCE_URL`           |
| `TRINO_USERNAME`                  | yes      |                             | `SPRING_DATASOURCE_USERNAME`      |
| `TRINO_PASSWORD`                  | yes²     |                             | `SPRING_DATASOURCE_PASSWORD`      |
| `SQL_ON_FHIR_URL`                 | no       |                             | `SQL_ON_FHIR_URL`                 |
| `QUERY_SQL_ON_FHIR_SCHEDULE_CRON` | no       |                             | `QUERY_SQL_ON_FHIR_SCHEDULE_CRON` |

¹ Required when `AUTH_ENABLED=true`.

² May be empty: Trino only accepts passwords over TLS, so a Trino without authentication needs an empty password.

Two more variables select the images: `RECRUIT_IMAGE_BASE_NAME` (default `ghcr.io/open-recruit/recruit`) and
`RECRUIT_IMAGE_TAG` (default: the release the Compose files belong to).

The `create-search-parameters` job only uses `FHIR_SERVER_BASE_URL`.

## Screening list

The screening list is an ASP.NET Core app. Nested settings use `__` as the separator in environment variable names,
e.g. `Auth__Oidc__Authority` sets `Oidc.Authority` inside the `Auth` section.

### FHIR server

- `FhirServer__BaseUrl` (required): base URL of the FHIR server, e.g. `https://fhir.example.com/fhir`.

### Database

The screening list stores user permissions, notification subscriptions and its job queue in PostgreSQL.

- `ConnectionStrings__AppDb` (required): an
  [Npgsql connection string](https://www.npgsql.org/doc/connection-string-parameters.html), e.g.
  `Host=postgres;Port=5432;Database=recruit;Username=recruit`.
- `PGUSER`, `PGPASSWORD`: database user and password, used if the connection string doesn't contain them.

### Authentication

- `Auth__Enabled` (default `false`): whether users sign in via OpenID Connect. When `false`, anyone who can reach the
  screening list is treated as a signed-in admin and can see every study and patient. Always set it to `true` in
  production.
- `Auth__AdminRole` (default `admin`): the role that can see every study and manage eligibility criteria and access
  permissions. Users without it only see the studies they've been given access to.
- `Auth__Oidc__Authority`: issuer URL of the OpenID Connect provider, e.g. `https://auth.example.com/realms/recruit`.
- `Auth__Oidc__ClientId`, `Auth__Oidc__ClientSecret`: the screening list's client credentials.
- `Auth__Oidc__RoleClaimType` (default `role`): the claim holding the user's roles.
- `Auth__Oidc__GroupClaimType` (default `groups`) and `Auth__Oidc__GroupRoleMappings`: grant a role to the members
  of an identity provider group. `GroupRoleMappings` maps group names to roles, e.g.
  `{ "/recruit-admins": "admin" }` in the `Auth:Oidc` section of the app's configuration.

Register the screening list as a confidential client with the OpenID Connect provider, with the redirect URI
`https://<screening list host>/signin-oidc`. The screening list requests the `openid`, `profile`, `email` and
`offline_access` scopes.

### Notifications

See [Notifications](notifications.md) for how notifications work. They are sent by email:

- `Notifications__Email__SmtpHost`, `Notifications__Email__SmtpPort` (default `25`): the SMTP server. TLS is
  negotiated automatically based on the port.
- `Notifications__Email__SmtpUsername`, `Notifications__Email__SmtpPassword`: SMTP credentials, if the server needs
  them.
- `Notifications__Email__From`: the sender address.
- `Notifications__Email__SubjectTemplate`: the subject line. `{0}` is replaced with the study's acronym.
- `Notifications__Email__PublicBaseUrl`: the URL users open the screening list at, e.g. `https://recruit.example.com/`.
  Every link in the emails is built from it, so it must be reachable from where your users read their email.

The screening list checks for new candidates every minute and sends due emails every 5 minutes. Neither interval is
configurable.

### Ports and observability

- `HTTP_PORT` (default `8080`): port of the web app.
- `METRICS_PORT` (default `8081`): port serving Prometheus metrics at `/metrics`.
- `Tracing__Enabled` (default `false`): export OpenTelemetry traces, configured via the standard variables like
  `OTEL_EXPORTER_OTLP_ENDPOINT`.

## Query module

The query module is a Spring Boot app. Its settings can be set as environment variables using Spring Boot's
[relaxed binding](https://docs.spring.io/spring-boot/reference/features/external-config.html#features.external-config.typesafe-configuration-properties.relaxed-binding.environment-variables),
e.g. `QUERY_SQL_ON_FHIR_SCHEDULE_CRON` for `query-sql-on-fhir.schedule.cron`.

### FHIR server access

- `FHIR_SERVER_BASE_URL` (required): base URL of the FHIR server.
- `FHIR_SERVER_TIMEOUT_IN_SECONDS` (default `300`): timeout of requests to the FHIR server.
- `FHIR_USE_UPDATE_AS_CREATE` (default `false`): write screening list entries using `PUT` with a deterministic id
  instead of conditional creates, for FHIR servers that don't support conditional creates.

### Criteria database

The database the eligibility criteria's SQL is run against.

- `SPRING_DATASOURCE_URL` (required): JDBC URL, e.g. `jdbc:trino://trino.example.com:443/fhir/default?SSL=true`.
  For Trino, it must include the catalog and schema holding the FHIR tables.
- `SPRING_DATASOURCE_USERNAME`, `SPRING_DATASOURCE_PASSWORD`: database credentials.
- `QUERY_SQL_ON_FHIR_SQL_DIALECT` (default `trino`): the database's SQL dialect, `trino` or `duckdb`. A criterion is
  run directly if its Library has `application/sql;dialect=<dialect>` content. For DuckDB, also set
  `SPRING_DATASOURCE_DRIVERCLASSNAME=org.duckdb.DuckDBDriver`, see
  [Querying FHIR resources with DuckDB](../trino/index.md#querying-fhir-resources-with-duckdb).
- `SQL_ON_FHIR_URL`: base URL of a SQL on FHIR server such as [Pathling](https://pathling.csiro.au/). Criteria that
  depend on ViewDefinitions or have no content in the configured dialect are sent to its `$sqlquery-run` operation.

### Schedule

- `QUERY_SQL_ON_FHIR_RUN_MODE` (default `scheduled`): `scheduled` runs forever on the schedule below. `one-shot` runs
  once and exits, with a non-zero exit code if any study failed, e.g. for running it as a Kubernetes Job.
- `QUERY_SQL_ON_FHIR_SCHEDULE_CRON` (default `@hourly`): when to update the screening lists, as a
  [Spring cron expression](https://docs.spring.io/spring-framework/reference/integration/scheduling.html#scheduling-cron-expression)
  with six fields (seconds first) or a macro like `@hourly`.

### Screening behavior

- `QUERY_SQL_ON_FHIR_REQUIRE_ALL_CRITERIA_MET` (default `false`): by default, patients for whom a criterion can't be
  evaluated because of missing data are also recommended, for manual review. When `true`, only patients who meet
  every criterion are recommended.
- `QUERY_SQL_ON_FHIR_MAX_RESEARCH_SUBJECTS` (default `-1`, unlimited): skip a study if it would recommend more
  patients than this.
- `QUERY_SQL_ON_FHIR_DRY_RUN` (default `false`): run the criteria and log how many patients would be recommended,
  without writing anything to the FHIR server.
- `QUERY_SQL_ON_FHIR_TRANSACTION_BUNDLE_CHUNK_SIZE` (default `500`): the maximum number of patients written to the
  FHIR server per transaction.
- `QUERY_SQL_ON_FHIR_FUNNEL_ENABLED` (default `false`): also compute each study's attrition funnel, i.e. how many
  patients remain after applying each criterion, shown in the screening list. It's a considerably more expensive query.
- `QUERY_SQL_ON_FHIR_FUNNEL_TOTAL_POPULATION_SQL` (default `SELECT COUNT(*) FROM Patient`): the query counting the
  funnel's total population, e.g. `SELECT COUNT(*) FROM fhir.default.patient`.

### Observability

- `OTEL_SDK_DISABLED` (default `true`): set to `false` to export OpenTelemetry traces, configured via the standard
  variables like `OTEL_EXPORTER_OTLP_ENDPOINT`.
- Health checks are served at `/actuator/health` and Prometheus metrics at `/actuator/prometheus` on port `8080`.
