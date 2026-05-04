FROM docker.io/cypress/included:14.5.4@sha256:848fb0d361178e695aa3ebd0f9632f2966232907c0fc02fbd6432e07d4d08d8b
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
