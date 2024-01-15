FROM docker.io/cypress/included:13.6.2@sha256:0f48995a7d06113a405534448a6bf6529e324d69a4b3e0e9b2fa02a8308ec2da
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
