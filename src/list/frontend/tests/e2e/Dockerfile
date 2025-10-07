FROM docker.io/cypress/included:15.4.0@sha256:94ce7218a0c1c3fa53e93955d82d39a0872bd32de3422bd1a98b6231f52fd889
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
