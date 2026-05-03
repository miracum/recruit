FROM docker.io/cypress/included:15.14.0@sha256:9e069952fedf00e73cadd4ee1e80404d92ba8552e5e7e49caee5c920075f24c6
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
