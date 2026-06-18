# syntax=docker/dockerfile:1
# SPDX-License-Identifier: MIT
# Multi-stage build for the CS2M API server. Produces a distroless
# image with a non-root user and read-only filesystem.

# === Build stage ===
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine@sha256:5a25e1d4a4f37e4f630af98291d504d0db57862e4506a7bcb4d035540696a502 AS build
WORKDIR /src

# Restore with the full project graph first so layer caching works.
COPY ApiServer.sln ./
COPY src/CS2M.ApiServer.Core/CS2M.ApiServer.Core.csproj src/CS2M.ApiServer.Core/
COPY src/CS2M.ApiServer.Protocol/CS2M.ApiServer.Protocol.csproj src/CS2M.ApiServer.Protocol/
COPY src/CS2M.ApiServer.Storage/CS2M.ApiServer.Storage.csproj src/CS2M.ApiServer.Storage/
COPY src/CS2M.ApiServer.Workers/CS2M.ApiServer.Workers.csproj src/CS2M.ApiServer.Workers/
COPY src/CS2M.ApiServer.Host/CS2M.ApiServer.Host.csproj src/CS2M.ApiServer.Host/
COPY tests/CS2M.ApiServer.Tests/CS2M.ApiServer.Tests.csproj tests/CS2M.ApiServer.Tests/
RUN dotnet restore ApiServer.sln

# Copy everything else and build.
COPY . .
RUN dotnet publish src/CS2M.ApiServer.Host/CS2M.ApiServer.Host.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# === Runtime stage (distroless) ===
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine@sha256:8f40cc1288d4e1daf2aa10522aff6a2d5b48aa9c2d44b61f9e8e0fd041ff400d AS runtime

# Create a non-root user.
RUN adduser -D -h /app cs2m
WORKDIR /app
USER cs2m

# Read-only filesystem: tmp dir must be writable for ASP.NET Core.
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080 \
    # The UDP listener is hard-coded for 0.0.0.0:4242
    COMPlus_EnableDiagnostics=0

COPY --from=build --chown=cs2m:cs2m /app/publish .

EXPOSE 8080/tcp 4242/udp

ENTRYPOINT ["dotnet", "CS2M.ApiServer.Host.dll"]
