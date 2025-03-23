# Contributing

All contributions are welcome!

## Setup for local development

From the `/src` directory:

```sh
docker compose -f hack/compose.yaml --profile=omop up
```

This will start all development dependencies:

- OHDSI WebAPI
- OHDSI ATLAS
- Broadsea Atlasdb - a pre-filled OMOP database
- Traefik
- HAPI FHIR JPA Server
- Jaeger
- MailDev
- Keycloak

If you want to start any of the recruIT modules as containers, you can specify the corresponding `--profile` switch.
For example, when working on the query module, it might be useful to run the screening list and the notify module
for debugging. The following will start all development dependencies as well as build and run the list and notify containers:

```sh
docker compose -f hack/compose.yaml --profile=omop --profile=notify --profile=list build
docker compose -f hack/compose.yaml --profile=omop --profile=notify --profile=list up
```

You can then start the query module via gradle by running

```sh
./gradlew :query:bootRun
```

### Setup for the Trino SQL-based query module

Use the `trino` profile to start the dependencies for using the Trino-based query module:

- Traefik
- HAPI FHIR JPA Server
- Jaeger
- MailDev
- Keycloak
- MinIO
- Pathling
- Hive Metastore
- Warehousekeeper (VACUUMs the Delta Lake tables and registers them in the Hive Metastore)
- Trino

```sh
docker compose -f hack/compose.yaml --profile=trino up
```

By default, this will also upload sample FHIR resources to the FHIR server and import the same
resources via Pathling into Delta Lake tables.

For development, you will also need to upload two FHIR SearchParameter resources required by the query module:

```sh
curl --fail-with-body -X POST -H "Content-Type: application/fhir+json" --data @hack/fhir/search-parameters-transaction.json "http://recruit-fhir-server.127.0.0.1.nip.io/fhir"
```

Afterwards you can upload a sample study with the associated SQL-encoded criteria:

```sh
curl --fail-with-body -X POST -H "Content-Type: application/fhir+json" --data @hack/fhir/trino-sql-study-transaction.json "http://recruit-fhir-server.127.0.0.1.nip.io/fhir"
```

Now, running

```sh
./gradlew :query-fhir-trino:bootRun
```

```sh
curl --fail-with-body "http://recruit-fhir-server.127.0.0.1.nip.io/fhir/List"
```

should create all appropriated resources to appear in the screening list.

## Building Container Images

### notify & query

Both the notify and query module are Java applications that can be build using Gradle.
Therefore, they also share the same Dockerfile for building the container and can optionally
also be built using [jib](https://github.com/GoogleContainerTools/jib).

#### Using Dockerfile

From the `/src` directory, run

```sh
export MODULE_NAME=query
docker build -t "ghcr.io/miracum/recruit/${MODULE_NAME}:local" --build-arg=MODULE_NAME=${MODULE_NAME} .
```

The `--build-arg` `MODULE_NAME` can be either `notify`, `query-fhir-trino`, or `query` (default).

#### Using jib

You can also build the container images using [jib](https://github.com/GoogleContainerTools/jib) via gradle:

```sh
export MODULE_NAME=query
./gradlew :${MODULE_NAME}:jibDockerBuild --image="ghcr.io/miracum/recruit/${MODULE_NAME}:local"
```

Using `jibDockerBuild` will build the image against the local docker daemon. Running just `./gradlew :query:jib`
will attempt to push the image to the remote registry.

### list

The list module is a NodeJS app with a Vue frontend and can be built via Dockerfile.
From the `/src/list` directory, run:

```sh
docker build -t ghcr.io/miracum/recruit/list:local .
```

## Skaffold

You can also directly build and deploy to a Kubernetes cluster for development:

Create a KinD cluster and install NGINX Ingress

```sh
kind create cluster --config=hack/k8s/kind-with-ingress-config.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/master/deploy/static/provider/kind/deploy.yaml
```

Run `skaffold dev`:

```sh
helm dep up ../charts/recruit/

skaffold dev
```
