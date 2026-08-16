FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build
WORKDIR /src

COPY RecruIT.List/RecruIT.List.csproj ./RecruIT.List/
COPY RecruIT.FhirConstants/RecruIT.FhirConstants.csproj ./RecruIT.FhirConstants/
RUN dotnet restore ./RecruIT.List/RecruIT.List.csproj

COPY . .

RUN dotnet publish ./RecruIT.List/RecruIT.List.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b
WORKDIR /app
USER $APP_UID

EXPOSE 8080/tcp
EXPOSE 8081/tcp

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "RecruIT.List.dll"]
