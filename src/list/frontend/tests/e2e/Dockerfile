FROM docker.io/cypress/included:15.7.1@sha256:6b621503593a984d3db1153cb34cc82dc9ae1f3cd554839d94f9ffde0662fe8e
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
