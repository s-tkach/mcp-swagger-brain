using System.ComponentModel.DataAnnotations;

namespace McpSwaggerBrain.Configuration;

public sealed class McpSwaggerBrainOptions
{
    public const string SectionName = "McpSwaggerBrain";

    [Required]
    public string DatabasePath { get; init; } = "./data/mcp-swagger-brain.db";

    [Required]
    public string EmbeddingModelPath { get; init; } = "./models/all-MiniLM-L6-v2.onnx";

    [Required]
    public string EmbeddingTokenizerPath { get; init; } = "./models/vocab.txt";

    public bool RefreshOnStartup { get; init; } = true;

    public IReadOnlyList<string> Sources { get; init; } = [];

    public string? ServerInstructions { get; init; }
}
