FROM docker.io/library/node:22.15.0-slim@sha256:557e52a0fcb928ee113df7e1fb5d4f60c1341dbda53f55e3d815ca10807efdce AS base
WORKDIR /app

COPY package.json ./
COPY package-lock.json ./

FROM base AS build-frontend
COPY frontend/package.json ./frontend/package.json

RUN npm clean-install --workspace=frontend

COPY frontend/ ./frontend/

ARG VERSION=v10.4.0 # x-release-please-version
ENV VUE_APP_VERSION=${VERSION} \
    NODE_ENV=production

RUN npm run build --workspace=frontend

FROM build-frontend AS test
COPY server/ ./server/
RUN npm run test:unit --workspace=frontend

FROM base AS build-server
COPY server/package.json ./server/
RUN npm clean-install --workspace=server
COPY server/*.js ./server/

FROM gcr.io/distroless/nodejs22-debian12:nonroot@sha256:581893ba58980f0b4c7444d7a16b89f4e966cd89a89f3e0c6f26c482e312f008
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

COPY --from=base /app/package*.json /app
