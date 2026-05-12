using System.ComponentModel.DataAnnotations;

namespace McpSwaggerKnowledge.Configuration;

public sealed class McpSwaggerKnowledgeOptions
{
    public const string SectionName = "McpSwaggerKnowledge";

    [Required]
    public string DatabasePath { get; init; } = "./data/mcp-swagger-knowledge.db";

    [Required]
    public string EmbeddingModelPath { get; init; } = "./models/all-MiniLM-L6-v2.onnx";

    [Required]
    public string EmbeddingTokenizerPath { get; init; } = "./models/vocab.txt";

    public bool RefreshOnStartup { get; init; } = true;

    public IReadOnlyList<string> Sources { get; init; } = [];

    public string? ServerInstructions { get; init; }
}
