using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SwaggerMcp.Configuration;
using SwaggerMcp.Embeddings;
using SwaggerMcp.Indexing;
using SwaggerMcp.Storage;
using SwaggerMcp.Tools;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    ["--appsettings"] = "AppsettingsPath"
});

var appsettingsPath = builder.Configuration["AppsettingsPath"];
if (!string.IsNullOrWhiteSpace(appsettingsPath))
{
    builder.Configuration.AddJsonFile(appsettingsPath, optional: false, reloadOnChange: false);
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddOptions<SwaggerMcpOptions>()
    .Bind(builder.Configuration.GetSection(SwaggerMcpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SwaggerMcpOptions>, SwaggerMcpOptionsValidator>();

builder.Services.AddHttpClient<SwaggerFetcher>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<SchemaSummarizer>();
builder.Services.AddSingleton<OpenApiChunker>();
builder.Services.AddSingleton<IEmbedder, OnnxEmbedder>();
builder.Services.AddSingleton<SqliteSchemaInitializer>();
builder.Services.AddSingleton<SqliteVectorSearch>();
builder.Services.AddSingleton<ISwaggerStore, SqliteSwaggerStore>();
builder.Services.AddSingleton<SwaggerIndexingService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SwaggerIndexingService>());

var serverInstructions = builder.Configuration[$"{SwaggerMcpOptions.SectionName}:{nameof(SwaggerMcpOptions.ServerInstructions)}"];

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "SwaggerMCP", Version = "1.0" };
        options.ServerInstructions = serverInstructions;
    })
    .WithStdioServerTransport()
    .WithTools<SwaggerTools>();

await builder.Build().RunAsync();
