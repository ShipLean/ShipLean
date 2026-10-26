using System.Text.Json;

namespace ShipLean.Application.Identity;

// The `recentProjects` user preference: a JSON array of project ids, most recent first. Anything that is not an id is skipped.
public static class RecentProjects
{
    public static IReadOnlyList<Guid> Parse(string? json)
    {
        if (json is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? [.. document.RootElement.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out _))
                .Select(v => Guid.Parse(v.GetString()!))]
            : [];
    }
}
