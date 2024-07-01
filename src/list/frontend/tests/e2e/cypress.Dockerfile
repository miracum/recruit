FROM docker.io/cypress/included:13.11.0@sha256:8b881991dc0716352a45a06a08b176b6a6d84a1d99e4d9ba15b4fe3a4f8f574c
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
