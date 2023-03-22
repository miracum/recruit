# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy@sha256:8af98f6e0b2cac12e38253f0963ff7e686a93982fbf89a07299d2c7426b88aff AS build
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

FROM mcr.microsoft.com/dotnet/nightly/runtime:7.0-jammy-chiseled@sha256:5783706779020d0f6b8f76530208a3692f09ea313be9984022a4016604583613 AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/bitnami/kubectl:1.26.3@sha256:617efc9e044efbbaa02e9b93eeca3b9b9facb757b20789bcdbc7bf19a4d489d7 /opt/bitnami/kubectl/bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
