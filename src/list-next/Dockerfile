FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:e1fc6e423f543119c406d24e2e687d67c569f18f04a37a8b0005d80ad0dcee80 AS build

WORKDIR /src

# Copy files that affect dependency resolution first.
# Changes to application source won't invalidate the restore layer.
COPY Directory.Build.props Directory.Packages.props ./

COPY RecruIT.List/RecruIT.List.csproj RecruIT.List/
COPY RecruIT.FhirConstants/RecruIT.FhirConstants.csproj RecruIT.FhirConstants/

# Persist NuGet packages between builds.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    --mount=type=cache,id=nuget-http,target=/root/.local/share/NuGet/v3-cache \
    dotnet restore RecruIT.List/RecruIT.List.csproj

# Now copy the actual source.
# Changes here don't invalidate the restore layer above.
COPY . .

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    --mount=type=cache,id=nuget-http,target=/root/.local/share/NuGet/v3-cache \
    dotnet publish RecruIT.List/RecruIT.List.csproj \
        --configuration Release \
        --output /app/publish \
        --no-restore \
        --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-resolute-chiseled-extra@sha256:0e8d291426c277e5b53bb99f3fa6d95c3b02eff20f7ca1d807a7608250164df3
WORKDIR /app

COPY --from=build /app/publish .

USER 1654:1654
ENTRYPOINT ["dotnet", "RecruIT.List.dll"]
