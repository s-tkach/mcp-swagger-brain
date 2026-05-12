using System.Text.Json;

namespace McpSwaggerKnowledge.Json;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
