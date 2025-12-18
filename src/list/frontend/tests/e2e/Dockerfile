FROM docker.io/cypress/included:15.8.1@sha256:ee41d089db6a482777a2356899cb1b5f7a86e271d2cb7f30c06c9172797e99d4
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
