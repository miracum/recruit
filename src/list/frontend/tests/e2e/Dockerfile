FROM docker.io/cypress/included:15.3.0@sha256:bcc9add4c54d0cf8165c0f3310737d86da6fb6ce6c33a314fd5e902fbf1f6c99
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
