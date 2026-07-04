# Keep this tag in lockstep with the @playwright/test version in package.json --
# the image ships browser binaries matching that exact version.
FROM mcr.microsoft.com/playwright:v1.61.1-noble
WORKDIR /root/e2e

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

CMD ["npm", "test"]
