# Keep this tag in lockstep with the @playwright/test version in package.json --
# the image ships browser binaries matching that exact version.
FROM mcr.microsoft.com/playwright:v1.63.0-noble@sha256:eff16c30e6f3f4af0a03fa4b706120d5e9b0891c344a27d64559aff5900a4a27
WORKDIR /root/e2e

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

CMD ["npm", "test"]
