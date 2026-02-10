FROM docker.io/cypress/included:15.10.0@sha256:c41a891e3a1e35ce0d98cb255465a8c43394ff1584cb3c9e40ba3aec2cdaa11b
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
