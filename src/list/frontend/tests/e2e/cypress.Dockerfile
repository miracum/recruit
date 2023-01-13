FROM docker.io/cypress/included:12.1.0@sha256:a5fa8546b93c55e1c09437e0d700eba3fc6e462692ee917392ed03772f9d3caf
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
