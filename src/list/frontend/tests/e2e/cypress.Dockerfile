FROM docker.io/cypress/included:13.4.0@sha256:fa0865c7dd496fef3951026e402faff7eca995fb6f8755b91a5943219717a787
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
