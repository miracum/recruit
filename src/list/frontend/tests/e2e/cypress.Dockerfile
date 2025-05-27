FROM docker.io/cypress/included:14.3.3@sha256:168d0d0f92a29a7d5650d2883832958b7d9f9c0a75a67d750041fd9cc7944678
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
