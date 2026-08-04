FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:3c0edbfe1549dd93fb789dc96299a40df865ad7bffefcaf38e8c05940686d641 AS build
WORKDIR /src

COPY Recruit.List.sln ./
COPY src/Recruit.List.Web/Recruit.List.Web.csproj src/Recruit.List.Web/
COPY tests/Recruit.List.Web.Tests/Recruit.List.Web.Tests.csproj tests/Recruit.List.Web.Tests/
RUN dotnet restore Recruit.List.sln

COPY src/ src/
COPY tests/ tests/

ARG VERSION=v10.5.7 # x-release-please-version
RUN dotnet build Recruit.List.sln -c Release --no-restore "-p:Version=${VERSION#v}"

FROM build AS test
RUN dotnet test Recruit.List.sln -c Release --no-build --no-restore --logger "console;verbosity=normal"

FROM build AS publish
ARG VERSION=v10.5.7 # x-release-please-version
RUN dotnet publish src/Recruit.List.Web/Recruit.List.Web.csproj -c Release --no-restore -o /app/publish "-p:Version=${VERSION#v}"

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled@sha256:85f9e72ebd7b94c939466070ea3321d9474fd99d3dc910ed0fc8d0374b2029e9
WORKDIR /app
# Port 8081 is currently not used, but exposed to make
# container-structure-tests across Java/Node/.NET modules
# more sharable. It might eventually be used to expose
# metrics on a dedicated port.
EXPOSE 8080/tcp 8081/tcp
# the chiseled base image ships without ICU; this app only ever formats using
# CultureInfo.InvariantCulture, so invariant globalization mode avoids pulling
# in the much larger "-extra" ICU-enabled base image.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=publish --chown=65532:65532 /app/publish ./

USER 65532:65532
ENTRYPOINT ["dotnet", "Recruit.List.Web.dll"]
