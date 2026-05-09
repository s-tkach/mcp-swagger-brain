using System.ComponentModel.DataAnnotations;

namespace SwaggerMcp.Configuration;

public sealed class SwaggerMcpOptions
{
    public const string SectionName = "SwaggerMcp";

    [Required]
    public string DatabasePath { get; init; } = "./data/swagger-mcp.db";

    [Required]
    public string EmbeddingModelPath { get; init; } = "./models/all-MiniLM-L6-v2.onnx";

    [Required]
    public string EmbeddingTokenizerPath { get; init; } = "./models/vocab.txt";

    public bool RefreshOnStartup { get; init; } = true;

    public IReadOnlyList<string> Sources { get; init; } = [];

    public string? ServerInstructions { get; init; }
}
