# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy@sha256:4099e5d6966436aa7cc37e9d2d5d0ab4b1e09abe9982d138a6a37f4ca696ce27 AS build
WORKDIR /build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY tester.csproj tester.csproj

RUN dotnet restore --runtime=linux-x64 tester.csproj

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

FROM mcr.microsoft.com/dotnet/nightly/runtime:7.0-jammy-chiseled@sha256:a246b2fd45c00abd9e6861b2a4cba5b14d13f091da72d9afc2e8b25bab218c90 AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/bitnami/kubectl:1.24.8@sha256:539065b76186a6091e814296a98965c65bc16eed9b6edd647628979d2a1eece5 /opt/bitnami/kubectl/bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
