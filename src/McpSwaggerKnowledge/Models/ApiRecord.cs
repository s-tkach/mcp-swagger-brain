namespace McpSwaggerKnowledge.Models;

public sealed record ApiRecord(
    long Id,
    string Name,
    string SourceUrl,
    string? BaseUrl,
    string? Title,
    string? Version,
    string? SpecHash,
    string? IndexedAt,
    long EndpointCount);
