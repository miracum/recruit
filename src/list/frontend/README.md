# list

> The FHIR-based Screening List Module

## Development

### Setup

```sh
npm install
# starts a FHIR-server and other services for testing.
# the FHIR-server comes preloaded with sample recommendations @ http://localhost:8082/fhir
docker-compose -f deploy/docker-compose.dev.yml up
```

The patient identifiers in [sample-record-1.json](deploy/data/sample-record-1.json) have been encrypted to show how
de-pseudonymization works.

The `docker-compose.dev.yml` contains the fhir-pseudonymizer service configured to decrypt these identifiers.

You can then enabled it by setting `DE_PSEUDONYMIZATION_ENABLED=1` before running `npm run server:watch`.

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

For development, a Keycloak server with a pre-configured test realm called "MIRACUM" is included in
`docker-compose.dev.yaml`.
It sets up a `uc1-screeninglist` client, representing this application. It also includes a few sample users to test the
access control:

- name: admin, password: admin (Keycloak Admin)
- name: user1, password: user1
- name: user2, password: user2
- name: user3, password: user3
- name: uc1-admin, password: admin (has the `admin` role in the `uc1-screeninglist` client and therefore allowed to
  access everything)

The repo also contains a set of sample authorization rules in [notify-rules.dev.yaml](../notify-rules.dev.yaml) which
are automatically loaded for development.

#### Disable Keycloak

For testing and development, it might be easier to disable Keycloak entirely. When running with `npm run serve`, you'll
need to modify [config-dev.json](public/config-dev.json) and set `isKeycloakDisabled` to `true`.
When running the server, you'll need to set the env var `KEYCLOAK_DISABLED=true`.

#### Export Keycloak realm config

When you make changes to the test realm, you can do the following to keep the included config up-to-date:

1. Within the Keycloak container run:

   ```sh
   cd /opt/jboss/keycloak/bin/
   standalone.sh -Dkeycloak.migration.action=export -Dkeycloak.migration.provider=singleFile -Dkeycloak.migration.file=/tmp/aio-export.json
   ```

1. Copy the export to the local FS/repo:

   ```sh
   docker cp deploy_keycloak_1:/tmp/aio-export.json ./deploy/data/aio-export.json
   ```

### Configure Table Columns

Editing the shown tablecolumns is possible. When running with `npm run serve`, you'll need to modify
[config-dev.json](public/config-dev.json) and set `hideDemographics`, `hideLastVisit` and/or `hideEhrButton` to `true`.

When running the server, you'll need to set the env vars `HIDE_DEMOGRAPHICS=true`, `HIDE_LAST_VISIT=true`, `HIDE_EHR_BUTTON=true`.
