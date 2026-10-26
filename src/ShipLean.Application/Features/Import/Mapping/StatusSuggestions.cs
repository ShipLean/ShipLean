using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Import.Mapping;

// Which ShipLean state each Jira status of each issue type lands in. In order: a state of the target type with the status's own
// name; else the first state of the target type in the status's category that no other status has taken (so Jira's "To Do" lands in
// "New"); else a new state named like the status, with the category Jira gives it. The first rule's states are taken before the second runs.
internal static class StatusSuggestions
{
    // Agile defaults, which a new project starts with: they count as existing states.
    private static readonly (string Name, StateCategory Category)[] Defaults =
    [
        ("New", StateCategory.Proposed), ("Active", StateCategory.InProgress), ("Resolved", StateCategory.Resolved), ("Closed", StateCategory.Completed),
    ];

    public static IReadOnlyList<StateRef> ExistingFor(WorkItemTypeId type, IReadOnlyList<StateDto>? project)
        => project is null
            ? [.. Defaults.Select(d => new StateRef(Guid.Empty, d.Name, d.Category))]
            : [.. project.Where(s => s.Type == type && s.Category != StateCategory.Removed).Select(s => new StateRef(s.Id, s.Name, s.Category))];

    // statuses: the Jira statuses of one issue type, in the order Jira lists them.
    public static IReadOnlyList<(string Name, string Target, StateCategory Category, bool Creates)> Suggest(
        IReadOnlyList<JiraStatus> statuses, IReadOnlyList<StateRef> existing)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, StateRef>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in statuses.Select(s => s.Name))
        {
            if (existing.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) is { } named)
            {
                byName[name] = named;
                taken.Add(named.Name);
            }
        }

        var result = new List<(string, string, StateCategory, bool)>();
        foreach (var status in statuses)
        {
            if (byName.TryGetValue(status.Name, out var named))
            {
                result.Add((status.Name, named.Name, named.Category, false));
                continue;
            }

            var category = TargetTypes.CategoryOf(status.Category);
            var free = existing.FirstOrDefault(s => s.Category == category && !taken.Contains(s.Name));
            if (free is not null)
            {
                taken.Add(free.Name);
                result.Add((status.Name, free.Name, free.Category, false));
            }
            else
            {
                result.Add((status.Name, status.Name, category, true));
            }
        }

        return result;
    }

    public static bool Exists(IReadOnlyList<StateRef> existing, string name)
        => existing.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
