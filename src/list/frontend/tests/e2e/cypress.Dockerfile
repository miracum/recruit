FROM docker.io/cypress/included:14.4.0@sha256:395d49855305ea82505c9fc026016c3e32147d17261077d361f9328432128d0a
WORKDIR /root
ENV CI=1

RUN npm install cypress-keycloak@1.9.0

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
