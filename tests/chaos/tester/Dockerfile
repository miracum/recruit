FROM mcr.microsoft.com/dotnet/sdk:10.0.103-noble@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a AS build
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

COPY --from=docker.io/rancher/kubectl:v1.35.2@sha256:25cae09dd56c549742c0ac0458f17553a724f261f4e8a119b56d93d7eeeeb638 /bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
