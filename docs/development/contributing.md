# Contributing

All contributions are welcome!

## Setup for local development

Change to the `/src` directory and run

```sh
docker compose -f hack/compose.yaml up
```

This will start these development fixtures:

- HAPI FHIR JPA Server
- Jaeger
- MailDev
- Keycloak
- MinIO
- Hive Metastore
- Trino

To additoinally start the OMOP-specific services, add `--profile=omop`:

```sh
docker compose -f hack/compose.yaml --profile=omop up
```

which starts:

- OHDSI WebAPI
- OHDSI ATLAS
- Broadsea Atlasdb - a pre-filled OMOP database

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

The `--build-arg` `MODULE_NAME` can be either `notify`, `query-sql-on-fhir`, or `query` (default).

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
