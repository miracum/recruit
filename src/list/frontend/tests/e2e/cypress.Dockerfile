FROM docker.io/cypress/included:12.13.0@sha256:bb18d50ddaad4c846c432ee5e6b83bbf239cfe8454d52b546dc2ceb57d4b60af
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
