FROM docker.io/cypress/included:12.17.4@sha256:658c7ba220b398d0dab9f474811ee8ef527424e5f841a9882d3ba6c0ad2acf9b
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
