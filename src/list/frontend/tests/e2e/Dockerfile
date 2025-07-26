FROM docker.io/cypress/included:14.5.3@sha256:c83bafd958f31f12add02cf7df35bf607390951ca6d80103fc7ac2498720cc16
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
