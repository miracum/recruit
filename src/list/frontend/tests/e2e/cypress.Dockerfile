FROM docker.io/cypress/included:12.7.0@sha256:f740d5f4320d73721123a1c254d864c8cdac8a730c69e8d2c49dfe87463b3c66
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
