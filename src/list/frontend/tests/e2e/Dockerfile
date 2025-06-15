FROM docker.io/cypress/included:14.4.1@sha256:e770b8644d36ebd3a5c51833f0147e70d1564c9c2b3ddc30cd23e12ceabdbc92
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
