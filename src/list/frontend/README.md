# list

> The FHIR-based Screening List Module

## Development

### Setup

```sh
npm install
```

This module's development dependencies (FHIR server, Keycloak, Jaeger, MailDev) are provided by the shared compose
stack in [`src/hack`](../../hack), used by all recruIT modules. From the `src` directory:

```sh
docker compose -f hack/compose.yaml up
```

This starts everything the list module needs — no `--profile` flag required. See
[Contributing](../../../docs/development/contributing.md) for the other available profiles (e.g. `omop` or `trino`)
used by other modules.

The FHIR server is reachable at <http://recruit-fhir-server.127.0.0.1.nip.io/fhir>. It starts out empty; see
[Loading sample recruitment-list data](#loading-sample-recruitment-list-data) below to populate it.

### Compiles and hot-reloads for development

```sh
npm run serve
```

### Compiles and minifies for production

```sh
npm run build
```

### Run your unit tests

```sh
npm run test:unit
```

### Run your end-to-end tests

```sh
npm run test:e2e
```

### Lints and fixes files

```sh
npm run lint
```

### Run the server component

```sh
# Build the static assets first. These are served by the server.
npm run build

# Run the actual server and auto-reload it whenever any JS file in the `/server/` dir is changed.
npm run server:watch

# Optional: The app uses pino for structured logging.
#           To prettify the output when debugging, run the following:
npm install -g pino-pretty
npm run server:watch | pino-pretty
```

#### Running server and client app in hot-reload mode at the same time

By default, the client-side app directly communicates with the FHIR server. To test de-pseudonymization and access
restrictions, you have to configure the following:

1. set `VUE_APP_FHIR_URL` in [.env.development](.env.development) to `http://localhost:8080/fhir`
1. run `npm run server:watch`
1. in a new terminal run `npm run serve`. The app should now be accessible on <http://localhost:8081/> and use the server
   as its backend.

#### Run E2E tests locally

E2E tests are run as part of the CI pipeline which tests the container and its dependencies in isolation. To replicate
this setup locally, i.e. if `npm run test:e2e` seems to work on your machine but not in the CI, try:

```sh
export CI_PROJECT_NAME=list-e2e
export CI_JOB_ID=locally
export IMAGE_TAG=test

docker build -t ghcr.io/miracum/recruit/list:${IMAGE_TAG} .

# this starts the FHIR server and pre-loads it with sample data
docker compose -p $CI_PROJECT_NAME-$CI_JOB_ID -f frontend/tests/e2e/docker-compose.yaml run loader

# runs the actual E2E tests by starting the container under test
docker compose -p $CI_PROJECT_NAME-$CI_JOB_ID -f frontend/tests/e2e/docker-compose.yaml run tester

# cleans up after the test
docker compose -p $CI_PROJECT_NAME-$CI_JOB_ID -f frontend/tests/e2e/docker-compose.yaml down -v --remove-orphans
```

You can find screenshots and videos of the E2E tests inside the [tests/e2e](tests/e2e) directory.

### Keycloak

For development, this module uses the shared Keycloak instance started as part of the [`src/hack`](../../hack)
compose stack, pre-configured with a `recruIT` realm (see
[recruit-realm-export.json](../../hack/keycloak/recruit-realm-export.json)) and a `recruit-list` client representing
this application. It includes a few sample users to test access control:

- name: admin, password: admin (Keycloak Admin, master realm)
- name: user1, password: user1
- name: user2, password: user2
- name: list-admin, password: list-admin (has the `admin` role in the `recruit-list` client and therefore allowed to
  access everything)

The repo also contains a set of sample authorization rules in [notify-rules.dev.yaml](../notify-rules.dev.yaml) which
are automatically loaded for development.

#### Disable Keycloak

For testing and development, it might be easier to disable Keycloak entirely. When running with `npm run serve`, you'll
need to modify [config-dev.json](public/config-dev.json) and set `isKeycloakDisabled` to `true`.
When running the server, you'll need to set the env var `KEYCLOAK_DISABLED=true`.

#### Loading sample recruitment-list data

The shared FHIR server starts out empty. To quickly populate the screening list UI without running the full
OMOP/Atlas/query pipeline (see [Contributing](../../../docs/development/contributing.md)), POST the same static
sample resources used by the E2E tests directly to it:

```sh
for f in deploy/data/sample-record-1.json deploy/data/sample-record-2.json \
  deploy/data/sample-record-3.json deploy/data/sample-lists.json; do
  curl --fail-with-body -X POST -H "Content-Type: application/fhir+json" \
    --data "@${f}" "http://recruit-fhir-server.127.0.0.1.nip.io/fhir"
done
```

The patient identifiers in [sample-record-1.json](deploy/data/sample-record-1.json) have been encrypted to show how
de-pseudonymization works; see [De-pseudonymization demo](#de-pseudonymization-demo) below.

#### De-pseudonymization demo

`src/hack` doesn't include the fhir-pseudonymizer service, since it's specific to this module's demo. Run it
standalone, alongside the shared stack:

```sh
docker run --rm --network=hack_default -p 5000:8080 \
  -v "$(pwd)/deploy/anonymization.yaml:/etc/anonymization.yaml:ro" \
  -e APIKEY=fhir-pseudonymizer-api-key \
  ghcr.io/miracum/fhir-pseudonymizer:v2.28.0@sha256:71ae4c5b0353095d615775fb07863e23322f8fe7cf13e6ab80cf083c77e4c03b
```

Then set `DE_PSEUDONYMIZATION_ENABLED=1` before running `npm run server:watch`.

#### Export Keycloak realm config

The Keycloak realm configuration is maintained centrally at
[`src/hack/keycloak/recruit-realm-export.json`](../../hack/keycloak/recruit-realm-export.json). See
[Contributing](../../../docs/development/contributing.md) for how to run the shared stack and make/export changes.

### Configure Table Columns

Editing the shown tablecolumns is possible. When running with `npm run serve`, you'll need to modify
[config-dev.json](public/config-dev.json) and set `hideDemographics`, `hideLastVisit` and/or `hideEhrButton` to `true`.

When running the server, you'll need to set the env vars `HIDE_DEMOGRAPHICS=true`, `HIDE_LAST_VISIT=true`, `HIDE_EHR_BUTTON=true`.
