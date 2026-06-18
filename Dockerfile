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

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "InfoTrack.Api.dll"]
