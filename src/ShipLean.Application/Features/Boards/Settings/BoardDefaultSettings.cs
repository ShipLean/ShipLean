using System.Text;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Boards.Settings;

// IBoardSettingsStore.BuildDefaults: the default document of a new board.
internal static class BoardDefaultSettings
{
    public static BoardSettingsJson Build(BoardLevel level, IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType)
    {
        return level == BoardLevel.Taskboard ? Taskboard() : Level(level, statesByType);
    }

    public static IReadOnlyList<WorkItemTypeId> TypesShown(BoardLevel level) => level switch
    {
        BoardLevel.Stories => [WorkItemTypeId.Story, WorkItemTypeId.Bug],
        BoardLevel.Features => [WorkItemTypeId.Feature],
        BoardLevel.Epics => [WorkItemTypeId.Epic],
        _ => [WorkItemTypeId.Task, WorkItemTypeId.Story, WorkItemTypeId.Bug],
    };

    private static BoardSettingsJson Level(BoardLevel level, IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType)
    {
        var types = TypesShown(level);
        var primary = types[0];
        var live = types.ToDictionary(t => t, t => Live(statesByType, t));
        var empty = types.Where(t => live[t].Count == 0).Cast<WorkItemTypeId?>().FirstOrDefault();
        if (empty is not null)
        {
            throw new ArgumentException($"The {empty.Value.ToString().ToLowerInvariant()} type has no live state, so a {level} board cannot be built.", nameof(statesByType));
        }
        var columns = live[primary].Select(s => new DraftColumn(s.Name, s.Category, new Dictionary<WorkItemTypeId, Guid> { [primary] = s.Id })).ToList();
        foreach (var other in types.Skip(1))
        {
            MapOther(columns, primary, other, live[other]);
        }
        FillGaps(columns, types, live);
        var ids = ColumnIds(columns.Select(c => c.Name).ToList());
        var json = columns
            .Select((c, i) => new BoardColumnJson(ids[i], c.Name, null, false, string.Empty, types.Select(t => new ColumnStateMapping(t, c.States[t])).ToList()))
            .ToList();
        var lane = new SwimlaneJson("lane-default", "Default lane", null, SwimlaneKind.Default, null);
        var expanded = level != BoardLevel.Stories;
        return new BoardSettingsJson(
            json, [lane], [CardFields(primary), .. types.Skip(1).Select(CardFields)],
            [], [], new CardAnnotationsJson(Tasks: !expanded, Stories: expanded, Bugs: expanded, Features: expanded), CardReordering.ChangesBacklogOrder);
    }

    // The k-th state of a category goes to the k-th column of that category; a state with no column left (Bug Triaged after Bug New
    // when Story has one Proposed state) gets a column of its own after the category's last column, named after the state, which
    // shows the primary type's nearest earlier state.
    private static void MapOther(List<DraftColumn> columns, WorkItemTypeId primary, WorkItemTypeId other, IReadOnlyList<StateRef> states)
    {
        foreach (var group in states.GroupBy(s => s.Category))
        {
            foreach (var (state, k) in group.Select((s, i) => (s, i)))
            {
                var slots = columns.Where(c => c.Category == group.Key).ToList();
                if (k < slots.Count)
                {
                    slots[k].States[other] = state.Id;
                    continue;
                }
                var at = slots.Count > 0 ? columns.IndexOf(slots[^1]) + 1 : columns.Count(c => c.Category < group.Key);
                var inherited = at > 0 ? new Dictionary<WorkItemTypeId, Guid>(columns[at - 1].States) : [];
                inherited[primary] = at > 0 ? columns[at - 1].States[primary] : columns[0].States[primary];
                inherited[other] = state.Id;
                columns.Insert(at, new DraftColumn(state.Name, group.Key, inherited));
            }
        }
    }

    // A column no state of a type was mapped to repeats that type's nearest earlier state (or its first state).
    private static void FillGaps(List<DraftColumn> columns, IReadOnlyList<WorkItemTypeId> types, Dictionary<WorkItemTypeId, List<StateRef>> live)
    {
        foreach (var type in types)
        {
            Guid previous = live[type][0].Id;
            foreach (var states in columns.Select(c => c.States))
            {
                if (states.TryGetValue(type, out var id))
                {
                    previous = id;
                }
                else
                {
                    states[type] = previous;
                }
            }
        }
    }

    private sealed record DraftColumn(string Name, StateCategory Category, Dictionary<WorkItemTypeId, Guid> States);

    private static BoardSettingsJson Taskboard() => new(
        [], [],
        [
            new CardFieldSettingJson(WorkItemTypeId.Task, true, true, true, false, false, [FieldIds.Parent]),
            new CardFieldSettingJson(WorkItemTypeId.Story, true, true, false, true, false, [FieldIds.State]),
            new CardFieldSettingJson(WorkItemTypeId.Bug, true, true, false, true, false, [FieldIds.State]),
        ],
        [], [], new CardAnnotationsJson(Tasks: true, Stories: false, Bugs: false, Features: false), null);

    private static CardFieldSettingJson CardFields(WorkItemTypeId type) => type switch
    {
        WorkItemTypeId.Story => new CardFieldSettingJson(type, true, true, true, true, false, [FieldIds.Parent, FieldIds.Priority, FieldIds.Sprint]),
        WorkItemTypeId.Bug => new CardFieldSettingJson(type, true, true, true, true, false, [FieldIds.Parent, FieldIds.Severity, FieldIds.Priority]),
        WorkItemTypeId.Feature => new CardFieldSettingJson(type, true, true, true, true, false, [FieldIds.Parent]),
        _ => new CardFieldSettingJson(type, true, true, true, true, false, []),
    };

    private static List<StateRef> Live(IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType, WorkItemTypeId type)
        => statesByType.TryGetValue(type, out var states) ? states.Where(s => s.Category != StateCategory.Removed).OrderBy(s => s.Category).ToList() : []; // stable: state order within a category

    // Slug ids are for defaults only: a column added later gets an opaque id (col-<random>), and ids are never reused.
    // "col-" + the lower-cased name with runs of other characters as one dash; a repeated name gets -2, -3 ...
    private static List<string> ColumnIds(IReadOnlyList<string> names)
    {
        var used = new HashSet<string>();
        var ids = new List<string>();
        foreach (var name in names)
        {
            var slug = new StringBuilder();
            foreach (var c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    slug.Append(c);
                }
                else if (slug.Length > 0 && slug[^1] != '-')
                {
                    slug.Append('-');
                }
            }
            var baseId = "col-" + slug.ToString().Trim('-');
            var id = baseId;
            var n = 1;
            while (!used.Add(id))
            {
                n++;
                id = $"{baseId}-{n}";
            }
            ids.Add(id);
        }
        return ids;
    }
}
