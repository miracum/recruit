# Development

## Setup for local development

From the `/src` directory:

```sh
docker compose -f hack/docker-compose.yaml up
```

This will start all development dependencies:

- OHDSI WebAPI
- OHDSI ATLAS
- Broadsea Atlasdb - a pre-filled OMOP database
- Traefik
- HAPI FHIR JPA Server
- Jaeger
- MailDev

If you want to start any of the recruIT modules as containers, you can specify the corresponding `--profile` switch.
For example, when working on the query module, it might be useful to run the screening list and the notify module
for debugging. The following will start all development dependencies as well as build and run the list and notify containers:

```sh
docker compose -f hack/docker-compose.yaml --profile=notify --profile=list up
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

The `--build-arg` `MODULE_NAME` must be either `notify` or `query` (default).

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
