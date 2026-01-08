FROM docker.io/cypress/included:15.8.2@sha256:3f7a9f813589d07e99c31e8e9de4e8403554bcb62cddc7c30dffc86a1f0755f7
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
