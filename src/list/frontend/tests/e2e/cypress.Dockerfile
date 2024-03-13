FROM docker.io/cypress/included:13.7.0@sha256:508f932087925790b7111a8b38091bb74c26797634f0868c881a7e95c9d538d9
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
