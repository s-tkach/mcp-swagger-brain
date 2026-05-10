using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpSwaggerBrain.Configuration;
using McpSwaggerBrain.Embeddings;
using McpSwaggerBrain.Models;
using McpSwaggerBrain.Storage;

namespace McpSwaggerBrain.Indexing;

public sealed class SwaggerIndexingService(
    IOptions<McpSwaggerBrainOptions> options,
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
            return;
        }

        foreach (var sourceUrl in options.Value.Sources)
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
    }

    public async Task<IReadOnlyList<RefreshResult>> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        using var throttler = new SemaphoreSlim(4);
        var tasks = options.Value.Sources.Select(async sourceUrl =>
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

        return await Task.WhenAll(tasks);
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
            var fetched = await fetcher.FetchAsync(sourceUrl, cancellationToken);
            var document = chunker.Chunk(fetched);

            var existingHash = await store.GetSpecHashAsync(document.ApiName, cancellationToken);
            if (string.Equals(existingHash, fetched.Hash, StringComparison.Ordinal))
            {
                return new RefreshResult(document.ApiName, false, 0, 0, 0, null);
            }

            var embeddings = new Dictionary<EndpointChunk, float[]>();
            foreach (var endpoint in document.Endpoints)
            {
                embeddings[endpoint] = await embedder.EmbedAsync(endpoint.EmbeddingText, cancellationToken);
            }

            return await store.UpsertDocumentAsync(document, embeddings, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh swagger source {SourceUrl}", sourceUrl);
            return new RefreshResult(sourceUrl, false, 0, 0, 0, ex.Message);
        }
    }
}
