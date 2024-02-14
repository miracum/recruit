FROM docker.io/cypress/included:13.6.4@sha256:2bac8865f25128665d2184b1592623398b4428ea0e1c82e586c82fb65a7aa46b
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
