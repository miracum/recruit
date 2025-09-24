FROM docker.io/library/node:22.17.1-slim@sha256:2fa754a9ba4d7adbd2a51d182eaabbe355c82b673624035a38c0d42b08724854 AS base
WORKDIR /app

COPY package.json ./
COPY package-lock.json ./

FROM base AS build-frontend
COPY frontend/package.json ./frontend/package.json

RUN npm clean-install --workspace=frontend

COPY frontend/ ./frontend/

ARG VERSION=v10.4.3 # x-release-please-version
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

FROM gcr.io/distroless/nodejs22-debian12:nonroot@sha256:79c2a8243e0b0b6ca5eefdc5751aeed2cafadbb55a9ebf05d60aad574b1b2a8b
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
