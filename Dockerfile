# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies
COPY BlazorPortfolio/BlazorPortfolio.csproj BlazorPortfolio/
RUN dotnet restore BlazorPortfolio/BlazorPortfolio.csproj

# Copy source and publish
COPY BlazorPortfolio/ BlazorPortfolio/
RUN dotnet publish BlazorPortfolio/BlazorPortfolio.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Debug: verify _framework exists
RUN ls /app/publish/wwwroot/_framework/

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render injects $PORT at runtime; fall back to 8080 locally
ENTRYPOINT ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet BlazorPortfolio.dll
