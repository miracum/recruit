# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy@sha256:9341d0b266b5ad78c69d9d7ce13e0a08f9257602a2d9469c59a2fddf55932f42 AS build
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

FROM mcr.microsoft.com/dotnet/nightly/runtime:7.0-jammy-chiseled@sha256:ec44f5a31403c0a5b59605809965b8480195c830a2ba1dd2bca38b064dd1a860 AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/bitnami/kubectl:1.28.2@sha256:0b28a0ae15893822f211bb8501d192c7b52d1920c32e4b43734aa6d94d51477f /opt/bitnami/kubectl/bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
