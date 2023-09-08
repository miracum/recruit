FROM docker.io/cypress/included:12.17.4@sha256:102b34b9e4cb9895c44a74293c6931e7282535775045dd8b1c7608667a34c4b6
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
