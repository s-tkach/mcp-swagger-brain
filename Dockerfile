# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY McpSwaggerKnowledge.sln ./
COPY src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj src/McpSwaggerKnowledge/
COPY tests/McpSwaggerKnowledge.Tests/McpSwaggerKnowledge.Tests.csproj tests/McpSwaggerKnowledge.Tests/
RUN dotnet restore src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj

COPY src/McpSwaggerKnowledge src/McpSwaggerKnowledge
COPY models models
RUN dotnet publish src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj \
    --configuration Release \
    --no-restore \
    --output /out

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
ARG TARGETARCH
ARG SQLITE_VEC_VERSION=0.1.9
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl tar \
    && case "${TARGETARCH}" in \
        arm64) SQLITE_VEC_ARCH="aarch64" ;; \
        amd64) SQLITE_VEC_ARCH="x86_64" ;; \
        *) echo "Unsupported TARGETARCH=${TARGETARCH}" && exit 1 ;; \
       esac \
    && curl -fsSL "https://github.com/asg017/sqlite-vec/releases/download/v${SQLITE_VEC_VERSION}/sqlite-vec-${SQLITE_VEC_VERSION}-loadable-linux-${SQLITE_VEC_ARCH}.tar.gz" \
       | tar -xz -C /app vec0.so \
    && apt-get purge -y --auto-remove curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out ./

ENV SQLITE_VEC_EXTENSION_PATH=/app/vec0.so
ENTRYPOINT ["dotnet", "McpSwaggerKnowledge.dll"]
