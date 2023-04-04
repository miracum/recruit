FROM docker.io/cypress/included:12.9.0@sha256:ae2a4b05f4f84ff8ba20c301d8c1f52004862e59b52c8386b457e7e213adcf60
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
