# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BlazorPortfolio/ BlazorPortfolio/

RUN dotnet publish BlazorPortfolio/BlazorPortfolio.csproj \
    -c Release \
    -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render injects $PORT at runtime; fall back to 8080 locally
ENTRYPOINT ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet BlazorPortfolio.dll
