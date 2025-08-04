FROM docker.io/cypress/included:14.5.3@sha256:ddf238e00773ae09c4e034ccf481adb1c8603d977f7abddf640e2a866f8939eb
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
