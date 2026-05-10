# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore, build, test
dotnet restore
dotnet build
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~OpenApiChunkerTests"

# Build/test without downloading the ONNX model (CI / no-runtime builds)
dotnet build /p:SkipEmbeddingModelDownload=true
dotnet test /p:SkipEmbeddingModelDownload=true

# Run the server locally
dotnet run --project src/McpSwaggerBrain/McpSwaggerBrain.csproj
```

The ONNX model (`models/all-MiniLM-L6-v2.onnx`, ~90 MB) is downloaded and SHA-256-verified automatically during build when missing. It is git-ignored. The URL and expected hash are pinned in `src/McpSwaggerBrain/McpSwaggerBrain.csproj`.

## Architecture

This is a .NET 10 MCP (Model Context Protocol) server that indexes OpenAPI/Swagger documents and exposes semantic search tools over them.

### Data flow

1. **Startup** — `SwaggerIndexingService` (a `BackgroundService`) calls `ISwaggerStore.InitializeAsync`, then fetches and indexes every configured source if `RefreshOnStartup` is true.
2. **Fetching** — `SwaggerFetcher` downloads raw swagger JSON and computes a content hash. If the hash matches what is stored, indexing is skipped.
3. **Chunking** — `OpenApiChunker` parses the OpenAPI document and produces one `EndpointChunk` per operation. Each chunk contains serialized parameters, request/response schemas, a `SchemaSummary` string, and a pre-built `EmbeddingText` (verb + path + summary + tags + schema summary).
4. **Embedding** — `OnnxEmbedder` runs `all-MiniLM-L6-v2` via ONNX Runtime to produce a 384-dimensional float vector for each chunk's `EmbeddingText`.
5. **Storage** — `SqliteSwaggerStore` upserts everything into SQLite. Embeddings go into either `endpoints_vec` (sqlite-vec virtual table) or `endpoints_vec_json` (JSON fallback), depending on `SqliteVectorMode`.

### Vector search modes

`SqliteVectorSearch` handles both paths transparently:

- **`SqliteVec` mode** — embeddings stored as binary in a `vec0` virtual table; `MATCH` query does the distance computation in SQLite. Enabled when `SQLITE_VEC_EXTENSION_PATH` points to a loadable `vec0` library. Docker image ships with `vec0.so`.
- **JSON fallback mode** — embeddings stored as JSON floats; candidates are loaded into C# and ranked by cosine similarity. Slower but works everywhere without native extensions.

`SqliteSchemaInitializer` creates both tables on startup; `SqliteVectorMode` (enum) is determined once and passed down.

### MCP tools

`SwaggerTools` (tagged `[McpServerToolType]`) implements the five public tools. It depends on `ISwaggerStore`, `IEmbedder`, and `SwaggerIndexingService`. The MCP server runs over stdio; logs go to stderr so stdout stays clean for MCP messages.

### Configuration

All options live under the `McpSwaggerBrain` JSON section (`McpSwaggerBrainOptions`). Configuration is loaded from `appsettings.json`; use `appsettings.example.json` as the template.

### Schema summarization

`SchemaSummarizer` produces a compact text summary of each endpoint's parameters and schemas (max depth 2, max 40 properties). This summary is embedded rather than the raw JSON to avoid bloating vectors. Full schema JSON is stored separately and returned by `get_endpoint_details`.

### Test project

Tests use xunit and live in `tests/McpSwaggerBrain.Tests`. `Fakes/`, `Fixtures/`, and `Support/` hold test infrastructure. Tests that exercise `OnnxEmbedder` or `SqliteSwaggerStore` need the ONNX model present at runtime; skip the download only for unit-only runs.
