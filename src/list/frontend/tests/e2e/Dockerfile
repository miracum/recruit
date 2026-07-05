# Keep this tag in lockstep with the @playwright/test version in package.json --
# the image ships browser binaries matching that exact version.
FROM mcr.microsoft.com/playwright:v1.61.1-noble@sha256:5b8f294aff9041b7191c34a4bab3ac270157a28774d4b0660e9743297b697e48
WORKDIR /root/e2e

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

CMD ["npm", "test"]
