FROM docker.io/library/node:18.7.0@sha256:a6f295c2354992f827693a2603c8b9b5b487db4da0714f5913a917ed588d6d41 AS build
WORKDIR /app

COPY package.json ./
COPY package-lock.json ./

RUN npm clean-install --omit=optional

COPY . .

ARG VERSION=0.0.0
ENV VUE_APP_VERSION=${VERSION} \
    NODE_ENV=production
RUN npm run build

FROM build AS test
RUN npm run test:unit

FROM build AS release
# Prune dependencies for production
RUN npm prune --omit=dev

FROM gcr.io/distroless/nodejs:18@sha256:1c03ff9d14da154b8d19fc28da20534b6d906be249a412cc43426b9d24350a55
WORKDIR /app
USER 65534:65534
EXPOSE 8080/tcp

COPY package.json ./
COPY package-lock.json ./
COPY server/ server/

COPY --from=release /app/node_modules node_modules
COPY --from=release /app/dist dist

CMD [ "/app/server/server.js" ]

ARG BUILD_TIME=""
LABEL maintaner="miracum.org" \
    org.opencontainers.image.created=${BUILD_TIME} \
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
