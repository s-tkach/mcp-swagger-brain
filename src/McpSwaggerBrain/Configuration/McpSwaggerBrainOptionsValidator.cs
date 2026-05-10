using Microsoft.Extensions.Options;

namespace McpSwaggerBrain.Configuration;

public sealed class McpSwaggerBrainOptionsValidator : IValidateOptions<McpSwaggerBrainOptions>
{
    public ValidateOptionsResult Validate(string? name, McpSwaggerBrainOptions options)
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
            errors.Add($"McpSwaggerBrain:Sources contains duplicate URLs: {string.Join(", ", duplicateUrls)}.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
