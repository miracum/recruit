FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build
WORKDIR /src

COPY list.csproj ./
RUN dotnet restore ./list.csproj

COPY . .

ARG VERSION=v10.5.7 # x-release-please-version
RUN dotnet publish ./list.csproj \
    -c Release \
    -o /app/publish \
    -p:Version=${VERSION#v} \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b
WORKDIR /app
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080/tcp

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "list.dll"]
