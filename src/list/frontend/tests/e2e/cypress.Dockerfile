FROM docker.io/cypress/included:13.8.0@sha256:210f67670c1b9ef0a7858cf0ffbedaccef46057580fcd08c50011a3f99c011de
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
