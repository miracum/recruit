FROM mcr.microsoft.com/dotnet/sdk:9.0.308-noble@sha256:7331fb5fb8798c58df1f263dc8cc843459965c00cfb29ebeb53bf4cc572d592d AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:9.0.11-noble-chiseled@sha256:399c4b0cb65bfb38e54249f596b62bc761edc67b7523bb476186973f00f429c5 AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/rancher/kubectl:v1.35.0@sha256:63a9f14d017459f47d23cb266b2857e82ffe6602d8955eef00ce111b2d8a61cf /bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
