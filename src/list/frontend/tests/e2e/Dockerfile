FROM docker.io/cypress/included:15.11.0@sha256:3d31c71a3f444c6905a724396ae6fe8c5099133207455fe36d2c6bf38446dc96
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
