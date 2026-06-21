FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/InfoTrack.Domain/InfoTrack.Domain.csproj src/InfoTrack.Domain/
COPY src/InfoTrack.Application/InfoTrack.Application.csproj src/InfoTrack.Application/
COPY src/InfoTrack.Infrastructure/InfoTrack.Infrastructure.csproj src/InfoTrack.Infrastructure/
COPY src/InfoTrack.Api/InfoTrack.Api.csproj src/InfoTrack.Api/

RUN dotnet restore src/InfoTrack.Api/InfoTrack.Api.csproj

COPY src/ src/

RUN dotnet publish src/InfoTrack.Api/InfoTrack.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

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
