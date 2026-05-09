using SwaggerMcp.Configuration;

namespace SwaggerMcp.Tests;

public sealed class SwaggerMcpOptionsValidatorTests
{
    private readonly SwaggerMcpOptionsValidator _validator = new();

    [Fact]
    public void Validate_AllowsEmptySources()
    {
        var result = _validator.Validate(null, new SwaggerMcpOptions());

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_RejectsDuplicateSourceUrls()
    {
        var result = _validator.Validate(null, new SwaggerMcpOptions
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
