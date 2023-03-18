FROM docker.io/cypress/included:12.8.1@sha256:f3bf4969ff6ad21395795a64f070a19f8eb33a3fa6967f211c0f606ff55f75ee
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
