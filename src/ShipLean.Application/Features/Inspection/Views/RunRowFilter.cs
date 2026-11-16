using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Views;

// The errors-only filter: the error rows and every ancestor of one, so a failed call still shows where it sat.
public static class RunRowFilter
{
    public static IReadOnlyList<RunPageRowDto> ErrorsWithAncestors(IReadOnlyList<RunPageRowDto> rows)
    {
        var byId = rows.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var keep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.Where(r => r.IsError))
        {
            var current = row;
            while (keep.Add(current.Id) && current.ParentId is { } parent && byId.TryGetValue(parent, out var up))
            {
                current = up;
            }
        }
        return [.. rows.Where(r => keep.Contains(r.Id))];
    }
}
