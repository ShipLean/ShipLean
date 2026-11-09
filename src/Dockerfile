# Build context is the repository root (db/ is embedded into ShipLean.Application):
#   docker build -f src/Dockerfile -t shiplean .

# Stage 1: build the SPA straight into the API project's wwwroot.
FROM node:24-alpine AS frontend
# pnpm at the version package.json's packageManager pins, through Node's own corepack.
RUN corepack enable
# The client may import design/tokens.css.
COPY design/ /source/design/
WORKDIR /source/src
# The workspace is rooted at src/. pnpm-workspace.yaml holds allowBuilds; without it pnpm fails on the dependency
# build scripts it hasn't been told about. Only the SPA and what it depends on are installed (the filter), so the
# phone app's dependencies never enter this image.
COPY src/package.json src/pnpm-workspace.yaml src/pnpm-lock.yaml ./
COPY src/ShipLean.Client/package.json ./ShipLean.Client/
RUN --mount=type=cache,id=pnpm,target=/root/.local/share/pnpm/store pnpm install --frozen-lockfile --filter shiplean-client...
COPY src/ShipLean.Client/ ./ShipLean.Client/
WORKDIR /source/src/ShipLean.Client
# Bundle only: CI's build job typechecks (tsc also covers tests).
RUN pnpm exec vite build

# Stage 2: publish the API with the SPA already inside wwwroot, so the
# published output is the complete app — no copy step in the runtime stage.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Stamped into AssemblyInformationalVersion ("1.0.0+<sha>") so /health names the
# running build. docker-publish.yml passes the commit; a local build says "unknown".
ARG SOURCE_COMMIT=unknown
WORKDIR /source
# Project files first: the restore layer only changes when they do.
COPY src/ShipLean.slnx src/Directory.Build.props src/
COPY src/ShipLean.WebApi/ShipLean.WebApi.csproj src/ShipLean.WebApi/
COPY src/ShipLean.Application/ShipLean.Application.csproj src/ShipLean.Application/
COPY src/ShipLean.Core/ShipLean.Core.csproj src/ShipLean.Core/
COPY src/ShipLean.Contracts/ShipLean.Contracts.csproj src/ShipLean.Contracts/
# The NuGet cache mount is empty on a fresh builder. GitHub Actions keeps the
# restore layer via the Actions cache; the mount itself does not survive the job.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/ShipLean.WebApi -r linux-x64
COPY src/ ./src/
COPY db/ ./db/
COPY --from=frontend /source/src/ShipLean.WebApi/wwwroot/ ./src/ShipLean.WebApi/wwwroot/
# Analyzers off here: CI already builds every project with them on and warnings as errors.
# No --no-restore: a cached restore layer does not contain the packages (they live in the mount,
# and the mount does not survive the job), so publish has to restore when the mount is empty.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/ShipLean.WebApi -c Release -r linux-x64 --self-contained false -o /app -p:SourceRevisionId=$SOURCE_COMMIT -p:RunAnalyzers=false

# Stage 3: the search embedding model (intfloat/multilingual-e5-small, int8 ONNX). Debian, not alpine: the fetch
# script needs curl, flock and sha256sum. A cache mount's files are not in the layer, so the model is copied out in
# the same RUN; the cache only saves the download when the script or manifest changes.
FROM debian:trixie-slim AS models
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY scripts/fetch-embedding-model.sh scripts/embedding-model.manifest scripts/shiplean-model.json /scripts/
RUN --mount=type=cache,id=models,target=/cache \
    /scripts/fetch-embedding-model.sh /cache/multilingual-e5-small \
    && mkdir -p /models \
    && cp -r /cache/multilingual-e5-small /models/multilingual-e5-small \
    && chmod -R a+rX /models

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Links the GHCR package to this repo when the image is published.
LABEL org.opencontainers.image.source="https://github.com/ShipLean/ShipLean"
# curl: the HEALTHCHECK below calls GET /health with it.
# postgresql-client-18 from PGDG: backups run pg_dump, which must be at least the server's major version (Postgres 18).
# The suite matches the base image's release (VERSION_CODENAME), and pg_dump --version fails the build if it's missing.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && install -d /usr/share/postgresql-common/pgdg \
    && curl -fsSL -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc https://www.postgresql.org/media/keys/ACCC4CF8.asc \
    && . /etc/os-release \
    && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt ${VERSION_CODENAME}-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client-18 \
    && rm -rf /var/lib/apt/lists/* \
    && pg_dump --version

WORKDIR /app
# Before the app, so a code change doesn't invalidate this layer. World-readable, so $APP_UID can load it.
COPY --from=models /models/multilingual-e5-small /app/models/multilingual-e5-small
COPY --from=build /app .
# SHIPLEAN_BLOB_ROOT in compose: owned by the app user, so the named volume mounted here starts writable.
RUN mkdir -p /data/blobs && chown $APP_UID /data/blobs

USER $APP_UID
# UTC everywhere (.claude/skills/utc-everywhere): the process's local zone, and every Postgres session's TimeZone.
ENV TZ=UTC PGTZ=UTC
ENV SHIPLEAN_EMBEDDINGS_MODEL_PATH=/app/models/multilingual-e5-small
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=60s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "ShipLean.WebApi.dll"]
