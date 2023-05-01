FROM docker.io/cypress/included:12.11.0@sha256:29dfeed99db7a9678a4402f9175c98074c23bbb5ad109058702bc401fc3cdd02
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
