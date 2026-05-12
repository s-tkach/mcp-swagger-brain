using Microsoft.Extensions.Options;

namespace McpSwaggerKnowledge.Configuration;

public sealed class McpSwaggerKnowledgeOptionsValidator : IValidateOptions<McpSwaggerKnowledgeOptions>
{
    public ValidateOptionsResult Validate(string? name, McpSwaggerKnowledgeOptions options)
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
            errors.Add($"McpSwaggerKnowledge:Sources contains duplicate URLs: {string.Join(", ", duplicateUrls)}.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
