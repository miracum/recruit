# Development

## Setup for local development

From the `/src` directory:

```sh
docker compose -f docker-compose.dev.yaml --profile=list-dev --profile=traefik up
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
