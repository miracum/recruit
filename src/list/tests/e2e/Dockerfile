# Keep this tag in lockstep with the @playwright/test version in package.json --
# the image ships browser binaries matching that exact version.
FROM mcr.microsoft.com/playwright:v1.62.1-noble@sha256:dcc5531e97840b9b5e794f2814476b21571c5124a3fca2267d73041f56e7580e
WORKDIR /root/e2e

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

CMD ["npm", "test"]
