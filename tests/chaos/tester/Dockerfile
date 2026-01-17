FROM mcr.microsoft.com/dotnet/sdk:10.0.102-noble@sha256:25d14b400b75fa4e89d5bd4487a92a604a4e409ab65becb91821e7dc4ac7f81f AS build
WORKDIR /build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY packages.lock.json tester.csproj ./

RUN dotnet restore --use-lock-file --runtime=linux-x64 tester.csproj

COPY . .

RUN <<EOF
dotnet build tester.csproj \
    --no-restore \
    --configuration=Release

dotnet publish tester.csproj \
    --no-restore \
    --no-build \
    --configuration=Release \
    -o /build/publish
EOF

FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-noble-chiseled@sha256:cc6a8adc9402e9c2c84423ee1a4c58a3098511ed5399804df0659eeafb0ae0cb AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/rancher/kubectl:v1.35.0@sha256:63a9f14d017459f47d23cb266b2857e82ffe6602d8955eef00ce111b2d8a61cf /bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
