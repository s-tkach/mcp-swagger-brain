using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpSwaggerKnowledge.Configuration;
using McpSwaggerKnowledge.Embeddings;
using McpSwaggerKnowledge.Models;
using McpSwaggerKnowledge.Storage;

namespace McpSwaggerKnowledge.Indexing;

public sealed class SwaggerIndexingService(
    IOptions<McpSwaggerKnowledgeOptions> options,
    SwaggerFetcher fetcher,
    OpenApiChunker chunker,
    IEmbedder embedder,
    ISwaggerStore store,
    ILogger<SwaggerIndexingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await store.InitializeAsync(stoppingToken);

        if (!options.Value.RefreshOnStartup)
        {
            logger.LogInformation("RefreshOnStartup is disabled; skipping initial indexing.");
            return;
        }

        var sources = options.Value.Sources;
        logger.LogInformation("Starting initial indexing of {Count} source(s).", sources.Count);

        foreach (var sourceUrl in sources)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var result = await RefreshSourceUrlAsync(sourceUrl, stoppingToken);
            if (result.Error is not null)
            {
                logger.LogWarning("Failed to index {Url}: {Error}", sourceUrl, result.Error);
            }
        }

        logger.LogInformation("Initial indexing complete.");
    }

    public async Task<IReadOnlyList<RefreshResult>> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        var sources = options.Value.Sources;
        logger.LogInformation("Refreshing all {Count} source(s).", sources.Count);

        using var throttler = new SemaphoreSlim(4);
        var tasks = sources.Select(async sourceUrl =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                return await RefreshSourceUrlAsync(sourceUrl, cancellationToken);
            }
            finally
            {
                throttler.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        logger.LogInformation("RefreshAll complete: {Succeeded} succeeded, {Failed} failed.",
            results.Count(r => r.Error is null), results.Count(r => r.Error is not null));
        return results;
    }

    public async Task<RefreshResult> RefreshAsync(string apiName, CancellationToken cancellationToken = default)
    {
        var api = await store.GetApiAsync(apiName, cancellationToken);
        if (api is null)
        {
            return new RefreshResult(apiName, false, 0, 0, 0, $"API '{apiName}' is not found.");
        }

        return await RefreshSourceUrlAsync(api.SourceUrl, cancellationToken);
    }

    private async Task<RefreshResult> RefreshSourceUrlAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Fetching {SourceUrl}...", sourceUrl);
            var fetched = await fetcher.FetchAsync(sourceUrl, cancellationToken);
            var document = chunker.Chunk(fetched);
            logger.LogInformation("Parsed '{ApiName}' — {EndpointCount} endpoint(s), hash {Hash}.",
                document.ApiName, document.Endpoints.Count, fetched.Hash[..8]);

            var existingHash = await store.GetSpecHashAsync(document.ApiName, cancellationToken);
            if (string.Equals(existingHash, fetched.Hash, StringComparison.Ordinal))
            {
                logger.LogInformation("'{ApiName}' is up-to-date (hash unchanged); skipping re-index.", document.ApiName);
                return new RefreshResult(document.ApiName, false, 0, 0, 0, null);
            }

            logger.LogInformation("Embedding {Count} endpoint(s) for '{ApiName}'...", document.Endpoints.Count, document.ApiName);
            var embeddings = new Dictionary<EndpointChunk, float[]>();
            foreach (var endpoint in document.Endpoints)
            {
                embeddings[endpoint] = await embedder.EmbedAsync(endpoint.EmbeddingText, cancellationToken);
            }

            var result = await store.UpsertDocumentAsync(document, embeddings, cancellationToken);
            logger.LogInformation(
                "Indexed '{ApiName}': +{Added} added, ~{Changed} changed, -{Removed} removed.",
                result.ApiName, result.Added, result.Changed, result.Removed);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh swagger source {SourceUrl}", sourceUrl);
            return new RefreshResult(sourceUrl, false, 0, 0, 0, ex.Message);
        }
    }
}
