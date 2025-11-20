FROM docker.io/cypress/included:15.7.0@sha256:554bfde1ce7e9d4a4893332ec0d18d28c0499299dcbb51346a81dd37c62bd642
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
