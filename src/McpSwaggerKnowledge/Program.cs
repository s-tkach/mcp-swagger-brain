using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using McpSwaggerKnowledge.Configuration;
using McpSwaggerKnowledge.Embeddings;
using McpSwaggerKnowledge.Indexing;
using McpSwaggerKnowledge.Storage;
using McpSwaggerKnowledge.Tools;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

config.AddEnvironmentVariables();

var rawSources = builder.Configuration["SWAGGER_SOURCES"];
if (!string.IsNullOrWhiteSpace(rawSources))
{
    var existingCount = builder.Configuration
        .GetSection("McpSwaggerKnowledge:Sources").GetChildren().Count();
    var inMemory = new Dictionary<string, string?>();
    var urls = rawSources.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    for (var i = 0; i < urls.Length; i++)
    {
        inMemory[$"McpSwaggerKnowledge:Sources:{existingCount + i}"] = urls[i];
    }
    config.AddInMemoryCollection(inMemory);
}
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddOptions<McpSwaggerKnowledgeOptions>()
    .Bind(builder.Configuration.GetSection(McpSwaggerKnowledgeOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<McpSwaggerKnowledgeOptions>, McpSwaggerKnowledgeOptionsValidator>();

builder.Services.AddHttpClient<SwaggerFetcher>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<SchemaSummarizer>();
builder.Services.AddSingleton<OpenApiChunker>();
builder.Services.AddSingleton<IEmbedder, OnnxEmbedder>();
builder.Services.AddSingleton<SqliteSchemaInitializer>();
builder.Services.AddSingleton<SqliteVectorSearch>();
builder.Services.AddSingleton<ISwaggerStore, SqliteSwaggerStore>();
builder.Services.AddSingleton<SwaggerIndexingService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SwaggerIndexingService>());

var serverInstructions = builder.Configuration.GetSection(McpSwaggerKnowledgeOptions.SectionName).GetValue<string>(nameof(McpSwaggerKnowledgeOptions.ServerInstructions));
if (string.IsNullOrWhiteSpace(serverInstructions))
{
    Console.Error.WriteLine($"ServerInstructions are not configured.");
}

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "mcp-swagger-knowledge", Version = "1.0" };
        options.ServerInstructions = serverInstructions;
    })
    .WithStdioServerTransport()
    .WithTools<SwaggerTools>();

await builder.Build().RunAsync();
