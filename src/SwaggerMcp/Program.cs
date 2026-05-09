using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using SwaggerMcp.Configuration;
using SwaggerMcp.Embeddings;
using SwaggerMcp.Indexing;
using SwaggerMcp.Storage;
using SwaggerMcp.Tools;

var appsettingsPath = new ConfigurationBuilder()
    .AddCommandLine(args, new Dictionary<string, string> { ["--appsettings"] = "AppsettingsPath" })
    .Build()["AppsettingsPath"];

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    
if (appsettingsPath is not null)
    config.AddJsonFile(appsettingsPath, optional: true);

config.AddEnvironmentVariables();
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

var serverInstructions = builder.Configuration.GetSection(SwaggerMcpOptions.SectionName).GetValue<string>(nameof(SwaggerMcpOptions.ServerInstructions));
if (string.IsNullOrWhiteSpace(serverInstructions))
{
    Console.Error.WriteLine($"ServerInstructions are not configured.");
}

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "SwaggerMCP", Version = "1.0" };
        options.ServerInstructions = serverInstructions;
    })
    .WithStdioServerTransport()
    .WithTools<SwaggerTools>();

await builder.Build().RunAsync();
