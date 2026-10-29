using System.Text.Json;
using Agoda.IoC.Core;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Boards.View;

// Decides which lanes a board shows for the current user. Stories and Features show their stored swimlanes unless the user
// turned them off ("off" or "none"). Features can instead show one lane per Epic ("epic"). Anything else is one lane.
[RegisterPerRequest]
public sealed class BoardLaneLayouts(BoardReads reads)
{
    public async Task<LaneLayout> BuildAsync(BoardSettingsDto board, EpicLookup epics, CancellationToken cancellationToken)
    {
        var preference = await reads.LanePreferenceAsync(board.Id, cancellationToken);
        var stored = board.Settings.Swimlanes;
        var collapsed = preference is "off" or "none";
        switch (board.Level)
        {
            case BoardLevel.Features when preference == "epic":
                return ByEpic(epics);
            case BoardLevel.Stories or BoardLevel.Features when !collapsed && stored.Count > 0:
                return new LaneLayout(LaneMode.Stored, await StoredAsync(board, cancellationToken));
            default:
                return new LaneLayout(LaneMode.Single, [Single(stored)]);
        }
    }

    // One lane per live Epic in backlog order (empty ones included), then No Epic last.
    public static LaneLayout ByEpic(EpicLookup epics) => new(LaneMode.ByEpic, EpicLanes(epics));

    // A board with lanes off, and the Epics board, hold every card in the default lane.
    private static LaneDef Single(IReadOnlyList<SwimlaneJson> stored)
    {
        var lane = stored.FirstOrDefault(l => l.Kind == SwimlaneKind.Default);
        return lane is null
            ? new LaneDef("lane-default", "Default lane", null, BoardLaneKind.Default, null, null, true, null)
            : new LaneDef(lane.Id, lane.Name, lane.Colour, BoardLaneKind.Default, null, null, true, null);
    }

    private async Task<IReadOnlyList<LaneDef>> StoredAsync(BoardSettingsDto board, CancellationToken cancellationToken)
    {
        var ids = board.Settings.Swimlanes
            .Where(l => l.Criteria is not null)
            .SelectMany(l => l.Criteria!.Clauses)
            .SelectMany(c => Values(c.Value))
            .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var names = await reads.NamesAsync(board.ProjectId, ids, cancellationToken);
        return [.. board.Settings.Swimlanes.Select(l => ToLane(l, names))];
    }

    private static IEnumerable<string?> Values(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().Select(AsText);
        }

        return [AsText(value)];
    }

    private static string? AsText(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static LaneDef ToLane(SwimlaneJson lane, IReadOnlyDictionary<Guid, string> names)
    {
        var kind = Enum.Parse<BoardLaneKind>(lane.Kind.ToString());
        if (lane.Kind != SwimlaneKind.Criteria || lane.Criteria is null)
        {
            // A criteria lane stored without criteria has nothing to write into a card.
            return new LaneDef(lane.Id, lane.Name, lane.Colour, kind, null, null, lane.Kind != SwimlaneKind.Criteria, null);
        }

        return new LaneDef(lane.Id, lane.Name, lane.Colour, kind, CriteriaWords.Describe(lane.Criteria, names), null, LaneWrites.CanDropInto(lane.Criteria), lane.Criteria);
    }

    private static List<LaneDef> EpicLanes(EpicLookup epics)
    {
        var lanes = epics.Rows
            .Where(r => r.Type == "epic" && r.StateCategory != "removed")
            .Select(r => new LaneDef(
                LaneLayout.EpicLaneId(r.Id), r.Title, null, BoardLaneKind.Epic, null,
                new ParentRefDto(r.Id, r.Key, new TypeRef(WorkItemTypeId.Epic, "Epic"), r.Title), true, null))
            .ToList();
        lanes.Add(new LaneDef(LaneLayout.NoEpicLaneId, "No Epic", null, BoardLaneKind.NoEpic, null, null, true, null));
        return lanes;
    }
}
