FROM docker.io/cypress/included:15.7.1@sha256:73b92d32caa163c1a11ad24a7ac579c18f0069c4acf92c3064c01f437979e575
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
