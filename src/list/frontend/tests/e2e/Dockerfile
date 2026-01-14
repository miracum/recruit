FROM docker.io/cypress/included:15.9.0@sha256:91111352c3adf4a0ff5bee6f3f49af56a66deb81d74a784dbb68bc98a3fbd26e
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
