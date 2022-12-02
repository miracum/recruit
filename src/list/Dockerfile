FROM docker.io/library/node:18.12.1@sha256:9ebe9845301ca70e879f22180dfff496b856bb5f2ec52397462a9f5f903ee654 AS base
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
