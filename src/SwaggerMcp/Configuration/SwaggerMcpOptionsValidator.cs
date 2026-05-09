using Microsoft.Extensions.Options;

namespace SwaggerMcp.Configuration;

public sealed class SwaggerMcpOptionsValidator : IValidateOptions<SwaggerMcpOptions>
{
    public ValidateOptionsResult Validate(string? name, SwaggerMcpOptions options)
    {
        var errors = new List<string>();

        var duplicateUrls = options.Sources
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .GroupBy(url => url, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateUrls.Count > 0)
        {
            errors.Add($"SwaggerMcp:Sources contains duplicate URLs: {string.Join(", ", duplicateUrls)}.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
