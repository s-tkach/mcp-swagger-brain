using McpSwaggerKnowledge.Configuration;

namespace McpSwaggerKnowledge.Tests;

public sealed class McpSwaggerKnowledgeOptionsValidatorTests
{
    private readonly McpSwaggerKnowledgeOptionsValidator _validator = new();

    [Fact]
    public void Validate_AllowsEmptySources()
    {
        var result = _validator.Validate(null, new McpSwaggerKnowledgeOptions());

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_RejectsDuplicateSourceUrls()
    {
        var result = _validator.Validate(null, new McpSwaggerKnowledgeOptions
        {
            Sources =
            [
                "https://billing.local/swagger.json",
                "https://BILLING.LOCAL/swagger.json"
            ]
        });

        Assert.True(result.Failed);
        Assert.Contains("duplicate URLs", string.Join('\n', result.Failures));
    }
}
