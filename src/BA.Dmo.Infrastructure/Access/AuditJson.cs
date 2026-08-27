using System.Text.Json;

namespace BA.Dmo.Infrastructure.Access;

internal static class AuditJson
{
    public static string? Normalize(string? payload)
    {
        if (payload is null)
            return null;

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                return payload;
        }
        catch (JsonException)
        {
        }

        return JsonSerializer.Serialize(payload);
    }
}
