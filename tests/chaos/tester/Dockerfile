# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy@sha256:60664521325f3a64d82ed352303aa22e60e5c1bbefefe045cac316079cee9296 AS build
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

FROM mcr.microsoft.com/dotnet/nightly/runtime:7.0-jammy-chiseled@sha256:13b720fc1ec799338aec10cb9a7a92811fcdfdf12b2a31ad18179f63b3a5c599 AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/bitnami/kubectl:1.26.2@sha256:e3370de1713bd2452c54abf45dddfbf04a95d431f8c738b4038e9b9b4a0965a7 /opt/bitnami/kubectl/bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
