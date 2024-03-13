FROM docker.io/cypress/included:13.6.6@sha256:376a360b6e75acb09cc13ca278ea1b6e7e95b14d0cf9134578c18d30115876b4
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
