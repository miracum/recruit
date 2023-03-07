FROM docker.io/library/node:18.14.2@sha256:8b2c008211854f4ee9ca328910d1c6bff8f30fc9fdf834b48f7ea40992a2079a AS base
WORKDIR /app

COPY package.json ./
COPY package-lock.json ./

FROM base AS build-frontend
COPY frontend/package.json ./frontend/package.json

RUN npm clean-install --workspace=frontend

COPY frontend/ ./frontend/

ARG VERSION=v10.0.8 # x-release-please-version
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

FROM gcr.io/distroless/nodejs:18@sha256:54015ec9ef0710c06d6159da0913cbaf63ab730bc94f6c2c4e86aaa0e9e02cb5
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
