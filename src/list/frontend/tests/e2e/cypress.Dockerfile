FROM docker.io/cypress/included:14.4.0@sha256:da668189f825f9e7fc778761fc7083a9f376b4443e9d219f98eb2483e0f1dc1e
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
