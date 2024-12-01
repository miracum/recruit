FROM docker.io/cypress/included:13.16.0@sha256:ba38ee9c44462f140b9df5a5c74f62a50e33ffcee17638a95b78ffbeff8eac1e
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
