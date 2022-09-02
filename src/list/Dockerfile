FROM docker.io/library/node:18.12.1@sha256:c47a2c61e635eb4938fcd56a1139b552300624b53e3eca06b5554a577f1842cf AS base
WORKDIR /app

COPY package.json ./
COPY package-lock.json ./

FROM base AS build-frontend
COPY frontend/package.json ./frontend/package.json

RUN npm clean-install --workspace=frontend

COPY frontend/ ./frontend/

ARG VERSION=0.0.0 # x-release-please-version
ENV VUE_APP_VERSION=${VERSION} \
    NODE_ENV=production
RUN npm run build --workspace=frontend

FROM build-frontend AS test
COPY server/ ./server/
RUN npm run test:unit --workspace=frontend

FROM base AS build-server
COPY server/package.json ./server/package.json
RUN npm clean-install --workspace=server
COPY server/*.js ./server/

FROM gcr.io/distroless/nodejs:18@sha256:4d7a90fba0aba8d143f011ded99268423b7bac5ea509e62216c9318eef2a7477
WORKDIR /app
USER 65532:65532
# Port 8081 is currently not used, but exposed to make
# container-structure-tests across Spring Boot/Node JS
# more sharable. It might eventually be used to expose
# metrics on a dedicated port.
EXPOSE 8080/tcp 8081/tcp
CMD [ "/app/server/index.js" ]

COPY --from=build-server /app/server server
COPY --from=build-server /app/node_modules server/node_modules
COPY --from=build-frontend /app/frontend/dist dist

# included for vulnerability scans
COPY package.json ./
COPY package-lock.json ./

ARG VERSION=0.0.0
ARG GIT_REF=""
LABEL maintaner="miracum.org" \
    org.opencontainers.image.authors="miracum.org" \
    org.opencontainers.image.vendor="miracum.org" \
    org.opencontainers.image.source="https://github.com/miracum/recruit" \
    org.opencontainers.image.url="https://miracum.github.io/recruit/" \
    org.opencontainers.image.documentation="https://miracum.github.io/recruit/" \
    org.opencontainers.image.version=${VERSION} \
    org.opencontainers.image.licenses="AGPL-3.0" \
    org.opencontainers.image.revision=${GIT_REF} \
    org.opencontainers.image.title="list" \
    org.opencontainers.image.description="Container image for the list module of the recruIT infrastructure"
