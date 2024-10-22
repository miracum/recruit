FROM docker.io/cypress/included:13.15.0@sha256:d2123cf33edc48d3e3f30bd24231459ca5291dcf66b91b0b30b50ffe568eb707
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
