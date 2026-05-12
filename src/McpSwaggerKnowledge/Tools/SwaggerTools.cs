using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using McpSwaggerKnowledge.Embeddings;
using McpSwaggerKnowledge.Indexing;
using McpSwaggerKnowledge.Json;
using McpSwaggerKnowledge.Storage;

namespace McpSwaggerKnowledge.Tools;

[McpServerToolType]
public sealed class SwaggerTools(
    ISwaggerStore store,
    IEmbedder embedder,
    SwaggerIndexingService indexingService,
    ILogger<SwaggerTools> logger)
{
    [McpServerTool(Name = "list_apis")]
    [Description("List configured and indexed APIs with title, version, endpoint count, and last index time.")]
    public async Task<IReadOnlyList<ApiSummaryDto>> ListApis(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tool invoked: list_apis");
        var apis = await store.ListApisAsync(cancellationToken);
        logger.LogInformation("list_apis returned {Count} API(s).", apis.Count);
        return apis.Select(api => new ApiSummaryDto(
            api.Name,
            api.Title,
            api.Version,
            api.EndpointCount,
            api.IndexedAt)).ToList();
    }

    [McpServerTool(Name = "get_endpoints")]
    [Description("Return a compact list of endpoints for one API. Optional tag and verb filters are supported.")]
    public async Task<IReadOnlyList<EndpointSummaryDto>> GetEndpoints(
        [Description("Configured API name, for example billing-service.")] string apiName,
        [Description("Optional OpenAPI tag filter.")] string? tag = null,
        [Description("Optional HTTP verb filter, for example GET or POST.")] string? verb = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tool invoked: get_endpoints — api={ApiName}, tag={Tag}, verb={Verb}", apiName, tag, verb);
        var endpoints = await store.GetEndpointsAsync(apiName, tag, verb, cancellationToken);
        logger.LogInformation("get_endpoints returned {Count} endpoint(s) for '{ApiName}'.", endpoints.Count, apiName);
        return endpoints.Select(endpoint => new EndpointSummaryDto(
            endpoint.Verb,
            endpoint.Path,
            endpoint.Summary,
            DeserializeTags(endpoint.TagsJson))).ToList();
    }

    [McpServerTool(Name = "search_endpoints")]
    [Description("Use this first when the user asks about any REST API endpoint or HTTP operation. Semantically searches indexed OpenAPI specs by natural-language query across endpoint purpose, path, params, request/response schemas.")]
    public async Task<IReadOnlyList<SearchHitDto>> SearchEndpoints(
        [Description("Natural-language search query, for example 'create invoice' or 'find users by email'.")] string query,
        [Description("Optional API name to limit search.")] string? apiName = null,
        [Description("Optional HTTP verb filter.")] string? verb = null,
        [Description("Maximum number of results.")] int top = 10,
        [Description("Minimum similarity score between 0 and 1. Results below this threshold are excluded. 0 returns all results.")] double minScore = 0.0,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tool invoked: search_endpoints — query='{Query}', api={ApiName}, verb={Verb}, top={Top}, minScore={MinScore}",
            query, apiName, verb, top, minScore);
        var embedding = await embedder.EmbedAsync(query, cancellationToken);
        var results = await store.SearchEndpointsAsync(embedding, apiName, verb, top, minScore, cancellationToken);
        logger.LogInformation("search_endpoints returned {Count} hit(s) for query '{Query}'.", results.Count, query);
        return results.Select(result => new SearchHitDto(
            result.ApiName,
            result.Verb,
            result.Path,
            result.Summary,
            result.Tags,
            Math.Round(result.Score, 4))).ToList();
    }

    [McpServerTool(Name = "get_endpoint_details")]
    [Description("Return full endpoint details including parameters, request body schema, responses, tags, and summaries.")]
    public async Task<EndpointDetailsDto?> GetEndpointDetails(
        [Description("Configured API name, for example billing-service.")] string apiName,
        [Description("HTTP verb, for example GET or POST.")] string verb,
        [Description("OpenAPI path, for example /invoices/{id}.")] string path,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tool invoked: get_endpoint_details — api={ApiName}, verb={Verb}, path={Path}", apiName, verb, path);
        var endpoint = await store.GetEndpointDetailsAsync(apiName, verb, path, cancellationToken);
        if (endpoint is null)
        {
            logger.LogWarning("get_endpoint_details: '{Verb} {Path}' not found in '{ApiName}'.", verb, path, apiName);
            return null;
        }

        return new EndpointDetailsDto(
            endpoint.ApiName,
            endpoint.Verb,
            endpoint.Path,
            endpoint.OperationId,
            endpoint.Summary,
            endpoint.Description,
            DeserializeTags(endpoint.TagsJson),
            DeserializeJson(endpoint.ParametersJson),
            endpoint.RequestSchemaJson is null ? null : DeserializeJson(endpoint.RequestSchemaJson),
            DeserializeJson(endpoint.ResponsesJson),
            endpoint.SchemaSummary);
    }

    [McpServerTool(Name = "delete_api")]
    [Description("Remove a previously indexed API and all its endpoints from the local index. Does not affect the source URL.")]
    public async Task<object> DeleteApi(
        [Description("Configured API name to remove.")] string apiName,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tool invoked: delete_api — api={ApiName}", apiName);
        var deleted = await store.DeleteApiAsync(apiName, cancellationToken);
        if (deleted)
        {
            logger.LogInformation("Deleted API '{ApiName}' from index.", apiName);
            return new { ApiName = apiName, Deleted = true };
        }

        logger.LogWarning("delete_api: API '{ApiName}' was not found in the index.", apiName);
        return new { ApiName = apiName, Deleted = false, Error = $"API '{apiName}' was not found in the index." };
    }

    [McpServerTool(Name = "refresh_api")]
    [Description("Re-fetch and re-index one configured API, or all APIs when apiName is omitted.")]
    public async Task<object> RefreshApi(
        [Description("Optional configured API name. Omit to refresh all APIs.")] string? apiName = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tool invoked: refresh_api — api={ApiName}", apiName ?? "<all>");
        if (string.IsNullOrWhiteSpace(apiName))
        {
            return await indexingService.RefreshAllAsync(cancellationToken);
        }

        return await indexingService.RefreshAsync(apiName, cancellationToken);
    }

    private static IReadOnlyList<string> DeserializeTags(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonDefaults.Web) ?? [];

    private static object? DeserializeJson(string json) =>
        JsonSerializer.Deserialize<object>(json, JsonDefaults.Web);
}

public sealed record ApiSummaryDto(
    string Name,
    string? Title,
    string? Version,
    long EndpointCount,
    string? IndexedAt);

public sealed record EndpointSummaryDto(
    string Verb,
    string Path,
    string? Summary,
    IReadOnlyList<string> Tags);

public sealed record SearchHitDto(
    string ApiName,
    string Verb,
    string Path,
    string? Summary,
    IReadOnlyList<string> Tags,
    double Score);

public sealed record EndpointDetailsDto(
    string ApiName,
    string Verb,
    string Path,
    string? OperationId,
    string? Summary,
    string? Description,
    IReadOnlyList<string> Tags,
    object? Parameters,
    object? RequestSchema,
    object? Responses,
    string SchemaSummary);
