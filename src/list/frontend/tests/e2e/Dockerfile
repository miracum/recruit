FROM docker.io/cypress/included:15.6.0@sha256:46bb4de6756baa895c41c671a0538cffea9e93300313e343138101048585d637
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
