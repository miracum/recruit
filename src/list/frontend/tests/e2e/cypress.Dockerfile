FROM docker.io/cypress/included:13.17.0@sha256:128490f34f1f2f7c0bab4dd1dbc4eaf0435ba99fe3e0f538d8ecb98b92e34d0f
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
