FROM docker.io/cypress/included:13.13.2@sha256:dd58e9bd00587ee6de52bef9400c159048e9e4eb82c67a73d1f54c95229b6677
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
