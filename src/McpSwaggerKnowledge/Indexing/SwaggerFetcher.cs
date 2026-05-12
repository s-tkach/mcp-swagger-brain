using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace McpSwaggerKnowledge.Indexing;

public sealed class SwaggerFetcher(HttpClient httpClient, ILogger<SwaggerFetcher> logger)
{
    public async Task<FetchedSwagger> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        logger.LogInformation("Fetched {Url} — {Bytes} bytes.", url, json.Length);
        return new FetchedSwagger(url, json, hash);
    }
}

public sealed record FetchedSwagger(string Url, string Json, string Hash);
