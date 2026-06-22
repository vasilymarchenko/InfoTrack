# --- Stage 1: build the Vue SPA ---
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
# DOCKER_BUILD=1 tells vite.config.ts to emit to ./dist instead of ../src/InfoTrack.Api/wwwroot
RUN DOCKER_BUILD=1 npm run build

# --- Stage 2: build the .NET API ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/InfoTrack.Domain/InfoTrack.Domain.csproj src/InfoTrack.Domain/
COPY src/InfoTrack.Application/InfoTrack.Application.csproj src/InfoTrack.Application/
COPY src/InfoTrack.Infrastructure/InfoTrack.Infrastructure.csproj src/InfoTrack.Infrastructure/
COPY src/InfoTrack.Api/InfoTrack.Api.csproj src/InfoTrack.Api/

RUN dotnet restore src/InfoTrack.Api/InfoTrack.Api.csproj

COPY src/ src/
# Inject the built SPA assets so they are published with the API
COPY --from=web /web/dist src/InfoTrack.Api/wwwroot/

RUN dotnet publish src/InfoTrack.Api/InfoTrack.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Npgsql loads libgssapi_krb5 for Kerberos/GSSAPI support; without it the
# library logs a non-fatal error on every startup. Install the package so the
# log stays clean. Password auth (used here) works either way.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Optional: trust extra CA certificates (e.g. a corporate SSL inspection proxy).
# Drop any *.crt files (PEM format) into certs/ before building — they are gitignored.
# No-op when certs/ is empty, so this Dockerfile works without modification elsewhere.
# See docs\ssl-corporate-proxy.md for details.
COPY certs/ /tmp/certs/
RUN if ls /tmp/certs/*.crt 2>/dev/null 1>&2; then \
    cp /tmp/certs/*.crt /usr/local/share/ca-certificates/ && \
    sed -i 's/\r//' /usr/local/share/ca-certificates/*.crt && \
    update-ca-certificates; \
    fi

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "InfoTrack.Api.dll"]
