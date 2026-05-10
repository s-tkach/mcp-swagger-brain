using McpSwaggerBrain.Configuration;

namespace McpSwaggerBrain.Tests;

public sealed class McpSwaggerBrainOptionsValidatorTests
{
    private readonly McpSwaggerBrainOptionsValidator _validator = new();

    [Fact]
    public void Validate_AllowsEmptySources()
    {
        var result = _validator.Validate(null, new McpSwaggerBrainOptions());

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_RejectsDuplicateSourceUrls()
    {
        var result = _validator.Validate(null, new McpSwaggerBrainOptions
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
