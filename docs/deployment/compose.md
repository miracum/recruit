# Docker Compose

recruIT ships two Compose files:

- `compose.yaml` contains only the recruIT modules: the screening list (`list`), the query module
  (`query-sql-on-fhir`), and a one-off job that installs recruIT's FHIR SearchParameters. It expects every other service
  to already exist and is the basis of a production installation.
- `compose.demo.yaml` adds everything those modules need to run on a single machine with sample data: a HAPI FHIR
  server, PostgreSQL, a Trino lakehouse filled with a handful of [Synthea](https://synthetichealth.github.io/synthea/)
  patients, a sample study, and a mock mail server. It's meant for trying recruIT out, not for real patient data. See
  the [Quickstart](../getting-started/installation.md) for how to run it.

## Getting the Compose files

Every [release](https://github.com/open-recruit/recruit/releases) has a `recruit-compose.tgz` asset. It contains the
`compose/` folder with the image tags pinned to that release:

```sh
tar -xzf recruit-compose.tgz
cd compose/
```

Alternatively, clone the repository and change into its `compose/` folder:

```sh
git clone https://github.com/open-recruit/recruit.git
cd recruit/compose/
```

The commands on this page are run from inside the `compose/` folder.

## Prerequisites

[Docker Engine](https://docs.docker.com/engine/install/) with the [Compose plugin](https://docs.docker.com/compose/)
version 5.6.0 or later (`docker compose`, not the legacy `docker-compose`).

## Production installation

For production, deploy only `compose.yaml` and connect it to services you run and operate yourself. Everything
`compose.demo.yaml` provides needs a production-grade replacement:

| Dependency                  | Used by             | Purpose                                               |
| --------------------------- | ------------------- | ----------------------------------------------------- |
| FHIR server                 | all modules         | Holds patients, studies, criteria and screening lists |
| Trino with a FHIR lakehouse | `query-sql-on-fhir` | Runs each study's eligibility criteria as SQL         |
| PostgreSQL                  | `list`              | Stores permissions and notification subscriptions     |
| OpenID Connect provider     | `list`              | Signs users in, e.g. Keycloak                         |
| SMTP server                 | `list`              | Sends notification emails about new candidates        |
| Reverse proxy with TLS      | `list`              | Serves the screening list over HTTPS                  |
| Pathling server (optional)  | `query-sql-on-fhir` | Runs criteria that Trino can't run directly           |

The modules work together like this:

1. The FHIR server holds your clinical data, at least the Patient resources, and the studies to screen for.
   Studies are ResearchStudy resources whose eligibility criteria are SQL queries stored in Library resources.
1. Separately from recruIT, the FHIR data is regularly exported and encoded as Delta Lake tables using
   [Pathling](https://pathling.csiro.au/), which Trino then queries.
1. On a schedule, `query-sql-on-fhir` reads the studies from the FHIR server, runs their criteria through Trino, and
   writes each study's candidates back to the FHIR server as a screening list.
1. `list` shows those screening lists and patient details from the FHIR server, and emails subscribed users about new
   candidates.

### FHIR server

recruIT needs a FHIR R4 server that supports:

- transaction Bundles with conditional and update-as-create `PUT` requests, since recruIT writes screening lists and
  studies this way
- custom SearchParameters together with `_include` and `_include:iterate`, which recruIT uses to fetch a study together
  with its criteria in a single request

The demo uses the [HAPI FHIR JPA server](https://github.com/hapifhir/hapi-fhir-jpaserver-starter).

The FHIR server must contain the Patient resources of the patients you screen, since the screening list displays their
names and demographics from there. If it also holds Encounter and Location resources, the screening list also shows each
patient's last known location.

The `create-search-parameters` job installs recruIT's SearchParameters on every start, so the modules need write
access to the FHIR server. Some FHIR servers only start indexing a new SearchParameter after a reindex or a restart. If
yours is one of them, do that once after the first installation.

### Trino and the FHIR lakehouse

`query-sql-on-fhir` doesn't query the FHIR server for clinical data. It runs each criterion's SQL through Trino against
the same FHIR data, encoded as [Delta Lake](https://delta.io/) tables by Pathling. You need:

- the FHIR resources encoded as Delta Lake tables in object storage (e.g. S3), using Pathling's encoding, with one table
  per resource type (`patient`, `condition`, `observation`, ...)
- a Hive metastore those tables are registered in
- Trino with a Delta Lake catalog on top of that metastore. Criteria refer to the tables as
  `<catalog>.<schema>.<table>`, so their SQL has to use the catalog and schema you choose. The sample criteria use
  `fhir.default`.

The tables must be built from the same FHIR server that recruIT is connected to. The query module refers to each
candidate as `Patient/<id>`, using the `id` column of the `patient` table, so those ids must match the Patient resources
in the FHIR server.

!!! warning "Keep the lakehouse in sync with the FHIR server"

    The patients and encounters in the lakehouse must match those in the FHIR server. If they can't be updated at the
    same time, the lakehouse may lag behind the FHIR server, but never the other way around:

    - If the lakehouse lags behind, a new patient is only recommended once their data has reached the lakehouse.
    - If the lakehouse is ahead, the query module may recommend patients the FHIR server doesn't know yet. Depending
      on whether the FHIR server checks references, writing their screening list entries then either fails, or the
      screening list shows them without a name and location.

    So load new data into the FHIR server first, and only then update the lakehouse from it, e.g. from a bulk export
    of the FHIR server.

Pathling's [`$import` operation](https://pathling.csiro.au/docs/server/operations/import) can load a
[FHIR bulk export](https://hl7.org/fhir/uv/bulkdata/) into Delta Lake tables. If your FHIR resources are streamed
through Kafka, <https://github.com/bzkf/fhir-to-lakehouse> does the same continuously. `compose.demo.yaml` shows a
complete, minimal setup using SeaweedFS as object storage. Refresh the tables regularly: recruIT can only recommend
patients whose data is already in the lakehouse.

See [Trino SQL-based Query Module](../trino/index.md) for more on the lakehouse and on writing criteria.

### Pathling server (optional)

A criterion runs directly on Trino if its Library has `application/sql;dialect=trino` content. Criteria that depend on
SQL on FHIR ViewDefinitions, or that only exist in another SQL dialect, are instead sent to the `$sqlquery-run`
operation of a SQL on FHIR server such as Pathling, configured via `SQL_ON_FHIR_URL`. Without it, studies with such
criteria fail to run.

### PostgreSQL

`list` stores its own data in a PostgreSQL database, set via `LIST_CONNECTIONSTRINGS_APPDB`. Create the database
beforehand. `list` creates and updates its tables itself on every start.

### Configuration

Create a `.env` file next to `compose.yaml`:

```sh
# FHIR server, as reachable from inside the containers
FHIR_SERVER_BASE_URL=https://fhir.example.com/fhir

# Trino
TRINO_JDBC_URL=jdbc:trino://trino.example.com:443/fhir/default?SSL=true
TRINO_USERNAME=recruit
TRINO_PASSWORD=change-me

# optional
SQL_ON_FHIR_URL=
QUERY_SQL_ON_FHIR_SCHEDULE_CRON=@hourly

# PostgreSQL for the screening list
LIST_CONNECTIONSTRINGS_APPDB=Host=postgres.example.com;Port=5432;Database=recruit;Username=recruit
PGPASSWORD=change-me

# sign-in via OpenID Connect
AUTH_ENABLED=true
AUTH_OIDC_AUTHORITY=https://auth.example.com/realms/recruit
AUTH_OIDC_CLIENT_ID=recruit-list
AUTH_OIDC_CLIENT_SECRET=change-me
AUTH_OIDC_ROLE_CLAIM_TYPE=role
```

See [Configuration options](../configuration/options.md#docker-compose) for what each variable sets and for all
other settings, including the [OpenID Connect client setup](../configuration/options.md#authentication).

### Mail server and port

`compose.yaml` doesn't publish a port for `list` and doesn't configure the mail server, since both depend on your
environment. Add them in a `compose.override.yaml` next to `compose.yaml`:

```yaml
services:
  list:
    ports:
      # only reachable from the host itself, i.e. through a reverse proxy running there
      - "127.0.0.1:8080:8080"
    environment:
      Notifications__Email__SmtpHost: smtp.example.com
      Notifications__Email__SmtpPort: "587"
      Notifications__Email__SmtpUsername: recruit
      Notifications__Email__SmtpPassword: change-me
      Notifications__Email__From: recruit@example.com
      Notifications__Email__ScreeningListLinkTemplate: https://recruit.example.com/trials/{0}/patients
```

See [Notifications](../configuration/options.md#notifications) for what these settings do.

### Reverse proxy

`list` serves plain HTTP on port 8080. Put it behind a reverse proxy that terminates TLS, such as
[Traefik](https://traefik.io/), [Caddy](https://caddyserver.com/) or nginx, and forward the `X-Forwarded-For`,
`X-Forwarded-Proto` and `X-Forwarded-Host` headers. `list` uses them to build the OpenID Connect redirect URI, so it
must be served over HTTPS for sign-in to work.

### Starting recruIT

```sh
docker compose --project-name=recruit \
    -f compose.yaml \
    -f compose.override.yaml up --detach
```

Compose reads `.env` from the current folder automatically. Check that the modules started and that
`create-search-parameters` exited successfully:

```sh
docker compose --project-name=recruit -f compose.yaml -f compose.override.yaml ps --all
docker compose --project-name=recruit -f compose.yaml -f compose.override.yaml logs query-sql-on-fhir
```

On every scheduled run, the query module logs `Polling for ResearchStudy resources referencing a Group with SQLQuery
Library`, followed by the studies it found and processed.
