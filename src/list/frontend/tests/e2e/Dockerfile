FROM docker.io/cypress/included:15.10.0@sha256:eef1ee7a29c24d4799d8abae145e2f1642016799d13cd74e0dc1b03a9d1dacc0
WORKDIR /root
ENV CI=1

COPY package*.json .

RUN npm clean-install

WORKDIR /root/e2e
# hadolint ignore=DL3002
USER 0:0
