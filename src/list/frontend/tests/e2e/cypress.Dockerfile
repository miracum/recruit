FROM docker.io/cypress/included:14.2.0@sha256:d48ba669f78ae18d0a874559548bb84fe6d64384118b2933c2ec5334f88cc175
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
