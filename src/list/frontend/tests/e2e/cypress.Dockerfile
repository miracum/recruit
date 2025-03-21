FROM docker.io/cypress/included:13.16.0@sha256:f6ffa776b521615921f52e32ce8d4fc88e8d01b8217b9e7196ce4c79ebb23218
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
