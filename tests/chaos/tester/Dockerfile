FROM mcr.microsoft.com/dotnet/sdk:9.0.303-noble@sha256:14fad15702d25fec7ff1532b762abd323fc7549c9b8a713b5871b88dc989053d AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:9.0.7-noble-chiseled@sha256:519d28b1f35e7060f5ae53d0e43e9522b59522dab51ecef6465af60b94b3ad92 AS runtime
WORKDIR /opt/chaos-tester
USER 65532:65532
ENV DOTNET_ENVIRONMENT="Production" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=docker.io/bitnami/kubectl:1.33.3@sha256:09e4cf3140642768ddbb65414a5c2da0019118d9a7c557540d6c63d7f67d7e67 /opt/bitnami/kubectl/bin/kubectl /usr/bin/kubectl

COPY ./sample-list-bundle.json /tmp/
COPY ./chaos.yaml /tmp/

COPY --chown=65532:65532 --from=build /build/publish .
ENTRYPOINT ["dotnet", "/opt/chaos-tester/tester.dll"]
