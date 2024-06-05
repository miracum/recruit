FROM docker.io/cypress/included:13.11.0@sha256:0a0788616af8be58e7c0223820b12ca5d70b4bb130952bbeeb214c02f51ba4b1
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
