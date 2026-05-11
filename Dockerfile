# Dockerfile для GuildOfGreed.Server.
#
# Multi-stage сборка:
#   1) sdk-образ собирает решение и публикует server (с подтянутым shared).
#   2) runtime-образ копирует только publish-output — итог тонкий.
#
# Сборка:    docker build -t guildofgreed-server .
# Запуск:    docker run -d --name gog-server -p 5870:5870 \
#               -v "$(pwd)/docker-data:/app/data" guildofgreed-server
# Логи:      docker logs -f gog-server
# Остановка: docker stop gog-server && docker rm gog-server
#
# Persistent data (SQLite + TLS cert) лежат в volume /app/data.

# ----- Build stage -----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Сначала только csproj-файлы для restore — даёт Docker-кэш на dependencies,
# который не инвалидируется при изменениях исходников.
COPY shared/GuildOfGreed.Shared.csproj shared/
COPY server/GuildOfGreed.Server.csproj server/
RUN dotnet restore server/GuildOfGreed.Server.csproj

# Теперь исходники и сборка.
COPY shared/ shared/
COPY server/ server/
RUN dotnet publish server/GuildOfGreed.Server.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ----- Runtime stage -----
FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Persistence: SQLite БД и self-signed TLS cert.
VOLUME /app/data
ENV GOG_DATA_DIR=/app/data

# Внутри контейнера слушаем на всех интерфейсах — наружу пробрасывается
# через `docker run -p host:5870`. Loopback не подходит: 127.0.0.1 внутри
# контейнера недоступен снаружи.
ENV GOG_HOST=0.0.0.0
ENV GOG_PORT=5870

EXPOSE 5870

ENTRYPOINT ["dotnet", "GuildOfGreed.Server.dll"]
