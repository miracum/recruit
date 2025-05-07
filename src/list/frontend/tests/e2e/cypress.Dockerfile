FROM docker.io/cypress/included:14.3.3@sha256:a33b6befcef4ce52056acd312461eabf6c3288a2fc24efb544054d306bc598de
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
