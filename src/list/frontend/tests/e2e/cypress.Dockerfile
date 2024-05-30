FROM docker.io/cypress/included:13.10.0@sha256:5b1d97ad6996ffa1611463d0e9720b0219b5c017f6107d6ca5090ee0d2b4d57f
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
