FROM docker.io/cypress/included:14.3.2@sha256:02fb6bdac6f41caa86b7140a5c361f6d85f25554b4992f0a8465a24f63187b79
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
