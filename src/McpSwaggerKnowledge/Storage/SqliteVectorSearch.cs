using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using McpSwaggerKnowledge.Json;
using McpSwaggerKnowledge.Models;

namespace McpSwaggerKnowledge.Storage;

public sealed class SqliteVectorSearch(ILogger<SqliteVectorSearch> logger)
{
    public Task UpsertEmbeddingAsync(
        SqliteConnection connection,
        IDbTransaction transaction,
        SqliteVectorMode mode,
        long endpointId,
        float[] embedding)
    {
        return mode == SqliteVectorMode.SqliteVec
            ? connection.ExecuteAsync("""
                DELETE FROM endpoints_vec WHERE rowid = @EndpointId;
                INSERT INTO endpoints_vec (rowid, embedding)
                VALUES (@EndpointId, @Embedding);
                """, new { EndpointId = endpointId, Embedding = SerializeVector(embedding) }, transaction) // vec0 doesn't support ON CONFLICT, so DELETE + INSERT is required
            : connection.ExecuteAsync($"""
                INSERT OR REPLACE INTO {SqliteSchemaInitializer.JsonFallbackTableName} (endpoint_id, embedding)
                VALUES (@EndpointId, @Embedding);
                """, new { EndpointId = endpointId, Embedding = SerializeVector(embedding) }, transaction);
    }

    public Task DeleteEmbeddingAsync(
        SqliteConnection connection,
        IDbTransaction transaction,
        SqliteVectorMode mode,
        long endpointId)
    {
        return mode == SqliteVectorMode.SqliteVec
            ? connection.ExecuteAsync("DELETE FROM endpoints_vec WHERE rowid = @EndpointId;", new { EndpointId = endpointId }, transaction)
            : connection.ExecuteAsync($"DELETE FROM {SqliteSchemaInitializer.JsonFallbackTableName} WHERE endpoint_id = @EndpointId;", new { EndpointId = endpointId }, transaction);
    }

    public async Task<IReadOnlyList<EndpointSearchResult>> SearchAsync(
        SqliteConnection connection,
        SqliteVectorMode mode,
        float[] embedding,
        string? apiName,
        string? verb,
        int top,
        double minScore = 0.0)
    {
        top = Math.Clamp(top, 1, 25);
        var results = mode == SqliteVectorMode.SqliteVec
            ? await SearchWithSqliteVecAsync(connection, embedding, apiName, verb, top)
            : await SearchWithJsonFallbackAsync(connection, embedding, apiName, verb, top);
        var filtered = minScore > 0.0 ? results.Where(r => r.Score >= minScore).ToList() : results;
        logger.LogInformation(
            "Vector search ({Mode}): {Total} candidate(s), {Returned} returned after minScore={MinScore} filter.",
            mode, results.Count, filtered.Count, minScore);
        return filtered;
    }

    private static async Task<IReadOnlyList<EndpointSearchResult>> SearchWithSqliteVecAsync(
        SqliteConnection connection,
        float[] embedding,
        string? apiName,
        string? verb,
        int top)
    {
        var rows = await connection.QueryAsync<SqliteVecSearchRow>("""
            SELECT
                a.name AS ApiName,
                e.verb AS Verb,
                e.path AS Path,
                e.summary AS Summary,
                e.tags AS TagsJson,
                v.distance AS Distance
            FROM endpoints_vec v
            JOIN endpoints e ON e.id = v.rowid
            JOIN apis a ON a.id = e.api_id
            WHERE v.embedding MATCH @Embedding
              AND k = @Top
              AND (@ApiName IS NULL OR a.name = @ApiName)
              AND (@Verb IS NULL OR e.verb = upper(@Verb))
            ORDER BY v.distance
            LIMIT @Top;
            """, new { Embedding = SerializeVector(embedding), ApiName = apiName, Verb = verb, Top = top });

        return rows
            .Select(row => new EndpointSearchResult(
                row.ApiName,
                row.Verb,
                row.Path,
                row.Summary,
                DeserializeTags(row.TagsJson),
                DistanceToCosineScore(row.Distance)))
            .ToList();
    }

    private static async Task<IReadOnlyList<EndpointSearchResult>> SearchWithJsonFallbackAsync(
        SqliteConnection connection,
        float[] embedding,
        string? apiName,
        string? verb,
        int top)
    {
        var rows = await connection.QueryAsync<JsonSearchRow>($"""
            SELECT
                a.name AS ApiName,
                e.verb AS Verb,
                e.path AS Path,
                e.summary AS Summary,
                e.tags AS TagsJson,
                v.embedding AS EmbeddingJson
            FROM endpoints e
            JOIN apis a ON a.id = e.api_id
            JOIN {SqliteSchemaInitializer.JsonFallbackTableName} v ON v.endpoint_id = e.id
            WHERE (@ApiName IS NULL OR a.name = @ApiName)
              AND (@Verb IS NULL OR e.verb = upper(@Verb));
            """, new { ApiName = apiName, Verb = verb });

        return rows
            .Select(row => new EndpointSearchResult(
                row.ApiName,
                row.Verb,
                row.Path,
                row.Summary,
                DeserializeTags(row.TagsJson),
                CosineSimilarity(embedding, DeserializeVector(row.EmbeddingJson))))
            .OrderByDescending(result => result.Score)
            .Take(top)
            .ToList();
    }

    private static string SerializeVector(float[] vector) =>
        JsonSerializer.Serialize(vector, JsonDefaults.Web);

    private static IReadOnlyList<string> DeserializeTags(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonDefaults.Web) ?? [];

    private static float[] DeserializeVector(string json) =>
        JsonSerializer.Deserialize<float[]>(json, JsonDefaults.Web) ?? [];

    private static double DistanceToCosineScore(double distance)
    {
        var score = 1 - (distance * distance / 2);
        return Math.Clamp(score, -1, 1);
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0;
        double leftLength = 0;
        double rightLength = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftLength += left[i] * left[i];
            rightLength += right[i] * right[i];
        }

        return leftLength <= 0 || rightLength <= 0
            ? 0
            : dot / (Math.Sqrt(leftLength) * Math.Sqrt(rightLength));
    }

    private sealed class SqliteVecSearchRow
    {
        public string ApiName { get; init; } = "";
        public string Verb { get; init; } = "";
        public string Path { get; init; } = "";
        public string? Summary { get; init; }
        public string TagsJson { get; init; } = "[]";
        public double Distance { get; init; }
    }

    private sealed class JsonSearchRow
    {
        public string ApiName { get; init; } = "";
        public string Verb { get; init; } = "";
        public string Path { get; init; } = "";
        public string? Summary { get; init; }
        public string TagsJson { get; init; } = "[]";
        public string EmbeddingJson { get; init; } = "[]";
    }
}
