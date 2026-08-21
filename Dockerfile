# syntax=docker/dockerfile:1

FROM node:22-alpine AS web-build
WORKDIR /src/apps/web

ARG NEXT_PUBLIC_PUBLIC_SITE_URL=http://border.com.tr
ENV NEXT_TELEMETRY_DISABLED=1 \
    NEXT_PUBLIC_PUBLIC_SITE_URL=$NEXT_PUBLIC_PUBLIC_SITE_URL

COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm install -g npm@12.0.2
RUN npm ci

COPY apps/web/ ./
RUN npm run build


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

COPY global.json ./
COPY src/Border.Domain/Border.Domain.csproj src/Border.Domain/
COPY src/Border.Application/Border.Application.csproj src/Border.Application/
COPY src/Border.Infrastructure/Border.Infrastructure.csproj src/Border.Infrastructure/
COPY src/Border.Api/Border.Api.csproj src/Border.Api/
RUN dotnet restore src/Border.Api/Border.Api.csproj

COPY src/ src/
RUN dotnet publish src/Border.Api/Border.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    && rm -f /app/publish/appsettings.Development*.json \
        /app/publish/appsettings.Testing*.json \
    && mkdir -p /app/publish/wwwroot

COPY --from=web-build /src/apps/web/out/ /app/publish/wwwroot/


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=backend-build /app/publish/ ./

RUN mkdir -p /app/App_Data/DataProtectionKeys \
    && chown -R "$APP_UID:$APP_UID" /app

USER $APP_UID

EXPOSE 10000

ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}; exec dotnet Border.Api.dll"]
