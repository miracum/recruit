# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy@sha256:f14046267eb12e0758a33af36ccd859ed61c170b2f861402e8f0ec3dd6301a8d AS build
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

FROM mcr.microsoft.com/dotnet/nightly/runtime:8.0-jammy-chiseled@sha256:892c4eb843936204c7f07e664fcea4c3bf1974859c2a7763012af11615c17e75 AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/bitnami/kubectl:1.28.3@sha256:1364cda0798b2c44f327265397fbd34a32e66d80328d6e50a2d10377d7e2ff6d /opt/bitnami/kubectl/bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
