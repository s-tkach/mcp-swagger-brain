using System.Text.Json;

namespace McpSwaggerBrain.Json;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
