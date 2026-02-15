FROM mcr.microsoft.com/dotnet/sdk:10.0.103-noble@sha256:0a506ab0c8aa077361af42f82569d364ab1b8741e967955d883e3f23683d473a AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0.3-noble-chiseled@sha256:3b0bd0fa83c55a73d85007ac6896b9e5ac61255d651be135b7d70622af56af78 AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/rancher/kubectl:v1.35.1@sha256:9e365be67e373fa097a429b6e8590caf7ddc17fb34bbea0837360619e2552916 /bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
