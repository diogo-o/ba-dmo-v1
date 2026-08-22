using System.Text.Json;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Shared.Identity;

/// <summary>
/// Parses the access_templates.modules jsonb column (Plan-V3 GLM-ACC-02:
/// [{ moduleId, capabilities: [] }]) into grant entries. Structural defects
/// fail explicitly (fail closed); semantically invalid entries are left for
/// the GrantNormalizer discard rules (U-04).
/// </summary>
public static class AccessTemplateGrantsParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Result<IReadOnlyList<ModuleGrant>, DomainError> Parse(string? modulesJson)
    {
        if (string.IsNullOrWhiteSpace(modulesJson))
            return Result<IReadOnlyList<ModuleGrant>, DomainError>.Success(
                Array.Empty<ModuleGrant>());

        List<ModulesEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<ModulesEntry>>(modulesJson, Options);
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<ModuleGrant>, DomainError>.Failure(
                DomainError.Unexpected(
                    "ACCESS_TEMPLATE_MODULES_INVALID",
                    $"Access template modules definition is not valid JSON: {ex.Message}"));
        }

        var grants = new List<ModuleGrant>();
        foreach (var entry in entries ?? new List<ModulesEntry>())
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.ModuleId))
                continue;

            grants.Add(new ModuleGrant(
                entry.ModuleId,
                entry.Capabilities?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList()
                    ?? new List<string>()));
        }

        return Result<IReadOnlyList<ModuleGrant>, DomainError>.Success(grants);
    }

    private sealed class ModulesEntry
    {
        public string? ModuleId { get; set; }

        public List<string>? Capabilities { get; set; }
    }
}
