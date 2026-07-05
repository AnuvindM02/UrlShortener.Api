# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies (layer caching)
COPY src/UrlShortener.Api/UrlShortener.Api.csproj ./UrlShortener.Api/
RUN dotnet restore ./UrlShortener.Api/UrlShortener.Api.csproj

# Copy everything else and publish
COPY src/UrlShortener.Api/ ./UrlShortener.Api/
RUN dotnet publish ./UrlShortener.Api/UrlShortener.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "UrlShortener.Api.dll"]
