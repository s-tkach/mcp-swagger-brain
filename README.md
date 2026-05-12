# MCP Swagger Brain

Local MCP server for indexing multiple `/swagger/v1/swagger.json` OpenAPI documents and exposing endpoint discovery tools to VS Code Copilot Chat and Claude Desktop.

The server stores parsed endpoint metadata in SQLite, uses `sqlite-vec` for vector search when available, and embeds operation summaries/schemas with a built-in `sentence-transformers/all-MiniLM-L6-v2` ONNX model.

## Requirements

- .NET 10 SDK for local development (`dotnet --list-sdks` should show a 10.x SDK).
- Docker, when running the packaged MCP server.
- The `all-MiniLM-L6-v2` ONNX model and `vocab.txt` tokenizer under `models/` when running from source.

The ONNX model is about 90 MB. Local builds download and verify it when missing, using the URL and SHA-256 pinned in `src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj`. CI or test-only builds that do not need runtime embedding assets can skip that work with `/p:SkipEmbeddingModelDownload=true`.

## Tools

- `list_apis` - list indexed APIs with title, version, endpoint count, and last index time.
- `get_endpoints(apiName, tag?, verb?)` - compact endpoint list for one API, with optional tag and verb filters.
- `search_endpoints(query, apiName?, verb?, top?, minScore?)` - semantic search across endpoint paths, summaries, parameters, and schema summaries.
- `get_endpoint_details(apiName, verb, path)` - full endpoint details including parameters, request body schema, responses, and tags.
- `delete_api(apiName)` - remove a previously indexed API and all its endpoints from the local index.
- `refresh_api(apiName?)` - re-fetch and re-index one configured API, or all APIs when omitted.

## Configure Sources

Copy `src/McpSwaggerKnowledge/appsettings.example.json` to your own `appsettings.json`, then edit the source list. When running Docker, mount your config into `/app/appsettings.json`:

```json
{
  "McpSwaggerKnowledge": {
    "DatabasePath": "./data/mcp-swagger-knowledge.db",
    "EmbeddingModelPath": "./models/all-MiniLM-L6-v2.onnx",
    "EmbeddingTokenizerPath": "./models/vocab.txt",
    "RefreshOnStartup": true,
    "ServerInstructions": "Use this server whenever the user asks about REST API endpoints, HTTP operations, request/response schemas, or how to call a specific API.",
    "Sources": [
      "https://petstore.swagger.io/v2/swagger.json",
      "https://fakerestapi.azurewebsites.net/swagger/v1/swagger.json"
    ]
  }
}
```

Swagger URLs are expected to be reachable without auth from the machine/container running the MCP server.

## Build Docker Image

```bash
docker build -t mcp-swagger-knowledge:latest .
```

The Docker image includes:

- .NET 10 runtime app
- built-in `all-MiniLM-L6-v2` ONNX model and tokenizer
- `sqlite-vec` loadable extension for the target Docker architecture

The ONNX model is downloaded during build only when missing, pinned by URL and SHA-256 in `src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj`, and copied into publish/Docker output. The downloaded `.onnx` file is ignored by git. Runtime startup fails fast if the configured model or tokenizer cannot be loaded.

## Run with Docker Compose

Create `~/Documents/mcp-swagger-knowledge/appsettings.json` (the `docker-compose.yml` mounts `${HOME}/Documents/mcp-swagger-knowledge/appsettings.json` by default), then run:

```bash
docker compose up -d
```

Useful commands:

```bash
docker compose logs -f mcp-swagger-knowledge
docker compose restart mcp-swagger-knowledge
docker compose down
```

The `data/` directory will be created automatically to store the SQLite database.

## VS Code Copilot

Use a user-level or workspace MCP config:

```json
{
  "servers": {
    "swagger": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/Users/<your-user>/Documents/mcp-swagger-knowledge/appsettings.json:/app/appsettings.json:ro",
        "-v", "/Users/<your-user>/Documents/mcp-swagger-knowledge/data:/app/data",
        "mcp-swagger-knowledge:latest"
      ]
    }
  }
}
```

You can also run directly from source with `dotnet`:

```json
{
  "servers": {
    "swagger": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/Users/<your-user>/Source/mcp-swagger-knowledge/src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj"
      ],
      "env": {
        "SWAGGER_SOURCES": "https://petstore.swagger.io/v2/swagger.json;https://fakerestapi.azurewebsites.net/swagger/v1/swagger.json"
      }
    }
  }
}
```

To use `sqlite-vec` when running from source, download the matching loadable extension for your OS/architecture and point the server at it with `SQLITE_VEC_EXTENSION_PATH`:

```json
{
  "servers": {
    "swagger": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/Users/<your-user>/Source/mcp-swagger-knowledge/src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj"
      ],
      "env": {
        "SWAGGER_SOURCES": "https://petstore.swagger.io/v2/swagger.json;https://fakerestapi.azurewebsites.net/swagger/v1/swagger.json",
        "SQLITE_VEC_EXTENSION_PATH": "/Users/<your-user>/Documents/mcp-swagger-knowledge/native/vec0.dylib"
      }
    }
  }
}
```

## Claude Desktop

Add this server to `claude_desktop_config.json`:

### With Docker

```json
{
  "mcpServers": {
    "swagger": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/Users/<your-user>/Documents/mcp-swagger-knowledge/appsettings.json:/app/appsettings.json:ro",
        "-v", "/Users/<your-user>/Documents/mcp-swagger-knowledge/data:/app/data",
        "mcp-swagger-knowledge:latest"
      ]
    }
  }
}
```

### With `dotnet run` from source

```json
{
  "mcpServers": {
    "swagger": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/Users/<your-user>/Source/mcp-swagger-knowledge/src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj"
      ],
      "env": {
        "SWAGGER_SOURCES": "https://petstore.swagger.io/v2/swagger.json;https://fakerestapi.azurewebsites.net/swagger/v1/swagger.json"
      }
    }
  }
}
```

## Local Development

```bash
dotnet restore
dotnet build
dotnet test

# Run with SWAGGER_SOURCES env var
export SWAGGER_SOURCES="https://petstore.swagger.io/v2/swagger.json;https://fakerestapi.azurewebsites.net/swagger/v1/swagger.json"
dotnet run --project src/McpSwaggerKnowledge/McpSwaggerKnowledge.csproj
```

The source path examples use the repository directory name `mcp-swagger-knowledge`; project and namespace names use `McpSwaggerKnowledge`.

When the MCP client starts the server, logs are written to stderr so stdout remains reserved for MCP stdio messages.

## Vector Search Modes

The Docker image includes the `sqlite-vec` extension and sets `SQLITE_VEC_EXTENSION_PATH=/app/vec0.so`, so Docker runs use `sqlite-vec` by default.

When running from source, `sqlite-vec` is used only when the native extension can be found. Set `SQLITE_VEC_EXTENSION_PATH` to the full path of the extracted `vec0` library, such as `vec0.dylib` on macOS, `vec0.so` on Linux, or `vec0.dll` on Windows. If the extension path is missing or the library cannot be loaded, the server uses the JSON fallback.

- `sqlite-vec` mode stores embeddings in the `endpoints_vec` virtual table and lets SQLite perform vector matching with `MATCH`, which is faster and better suited for larger indexes.
- JSON fallback mode stores embeddings as JSON text in `endpoints_vec_json`, loads matching rows into the application, and computes cosine similarity in process. It is slower because it scans candidates in C#, but it keeps local development and unsupported environments working without a native extension.

## Notes

Large request and response schemas are summarized deterministically before embedding to avoid bloated search records. Full schema details remain available through `get_endpoint_details`.

API names are taken directly from each OpenAPI document's `info.title` field, preserved as-is.
