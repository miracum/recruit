# Keep this tag in lockstep with the @playwright/test version in package.json --
# the image ships browser binaries matching that exact version.
FROM mcr.microsoft.com/playwright:v1.64.0-noble@sha256:06a9939e57531807f8d5fd76ce44b53165ffb7d7501d87ab10e285c20b1e971f
WORKDIR /root/e2e

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

CMD ["npm", "test"]
