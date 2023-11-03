FROM docker.io/library/node:21.1.0@sha256:0052410af98158173b17a26e0e2a46a3932095ac9a0ded660439a8ffae65b1e3 AS base
WORKDIR /app

COPY package.json ./
COPY package-lock.json ./

FROM base AS build-frontend
COPY frontend/package.json ./frontend/package.json

RUN npm clean-install --workspace=frontend

COPY frontend/ ./frontend/

ARG VERSION=v10.1.7 # x-release-please-version
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

FROM gcr.io/distroless/nodejs20-debian12:nonroot@sha256:5478cbb8b444062e48745de199a1b2607dc9a46669f353e1af25e44a2df92cf6
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
