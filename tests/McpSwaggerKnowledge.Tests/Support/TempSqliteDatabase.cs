using Microsoft.Extensions.Options;
using McpSwaggerKnowledge.Configuration;

namespace McpSwaggerKnowledge.Tests.Support;

internal sealed class TempSqliteDatabase : IAsyncDisposable
{
    private TempSqliteDatabase()
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        Options = Microsoft.Extensions.Options.Options.Create(new McpSwaggerKnowledgeOptions { DatabasePath = FilePath });
    }

    public string FilePath { get; }

    public IOptions<McpSwaggerKnowledgeOptions> Options { get; }

    public static TempSqliteDatabase Create() => new();

    public ValueTask DisposeAsync()
    {
        foreach (var path in new[] { FilePath, $"{FilePath}-shm", $"{FilePath}-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return ValueTask.CompletedTask;
    }
}
