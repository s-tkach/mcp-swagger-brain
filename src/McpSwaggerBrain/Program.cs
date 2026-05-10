using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using McpSwaggerBrain.Configuration;
using McpSwaggerBrain.Embeddings;
using McpSwaggerBrain.Indexing;
using McpSwaggerBrain.Storage;
using McpSwaggerBrain.Tools;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

config.AddEnvironmentVariables();

var rawSources = builder.Configuration["SWAGGER_SOURCES"];
if (!string.IsNullOrWhiteSpace(rawSources))
{
    var existingCount = builder.Configuration
        .GetSection("McpSwaggerBrain:Sources").GetChildren().Count();
    var inMemory = new Dictionary<string, string?>();
    var urls = rawSources.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    for (var i = 0; i < urls.Length; i++)
    {
        inMemory[$"McpSwaggerBrain:Sources:{existingCount + i}:Url"] = urls[i];
    }
    config.AddInMemoryCollection(inMemory);
}
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddOptions<McpSwaggerBrainOptions>()
    .Bind(builder.Configuration.GetSection(McpSwaggerBrainOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<McpSwaggerBrainOptions>, McpSwaggerBrainOptionsValidator>();

builder.Services.AddHttpClient<SwaggerFetcher>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<SchemaSummarizer>();
builder.Services.AddSingleton<OpenApiChunker>();
builder.Services.AddSingleton<IEmbedder, OnnxEmbedder>();
builder.Services.AddSingleton<SqliteSchemaInitializer>();
builder.Services.AddSingleton<SqliteVectorSearch>();
builder.Services.AddSingleton<ISwaggerStore, SqliteSwaggerStore>();
builder.Services.AddSingleton<SwaggerIndexingService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SwaggerIndexingService>());

var serverInstructions = builder.Configuration.GetSection(McpSwaggerBrainOptions.SectionName).GetValue<string>(nameof(McpSwaggerBrainOptions.ServerInstructions));
if (string.IsNullOrWhiteSpace(serverInstructions))
{
    Console.Error.WriteLine($"ServerInstructions are not configured.");
}

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "mcp-swagger-brain", Version = "1.0" };
        options.ServerInstructions = serverInstructions;
    })
    .WithStdioServerTransport()
    .WithTools<SwaggerTools>();

await builder.Build().RunAsync();
