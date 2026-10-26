using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Backlog.Shared;

// Turns stack rows into backlog rows. Tag colours are the matching board's, matched ignoring case.
public sealed class BacklogRows(IReadOnlyList<TagColourJson> tagColours)
{
    public IReadOnlyList<TagColourJson> TagColours => tagColours;

    private readonly Dictionary<string, Swatch> _colours =
        tagColours.GroupBy(t => t.Tag, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Colour, StringComparer.OrdinalIgnoreCase);

    public BacklogRowDto Item(StackRowDto row, int depth, int? rank) =>
        Build(row, BacklogRowKind.Item, depth, rank, [], null, false);

    // A Feature or Epic row in a level backlog: an item row that carries its roll-ups and the sum under it.
    public BacklogRowDto LevelItem(StackRowDto row, int rank, IReadOnlyList<RollupCountDto> rollups, decimal? childPoints) =>
        Build(row, BacklogRowKind.Item, 0, rank, rollups, childPoints, SumExceeds(row.Points, childPoints));

    public BacklogRowDto Group(StackRowDto row, int depth, IReadOnlyList<RollupCountDto> rollups, decimal? childPoints) =>
        Build(row, BacklogRowKind.Group, depth, null, rollups, childPoints, SumExceeds(row.Points, childPoints));

    public static BacklogRowDto NoParent(IReadOnlyList<RollupCountDto> rollups, decimal? childPoints) =>
        new(BacklogRowKind.NoParentGroup, Guid.Empty, string.Empty, null, "No parent", null, null, null, childPoints, false, null, null, 0, null, [], rollups, 0);

    // A Feature or Epic whose children's points exceed its own by more than a quarter.
    private static bool SumExceeds(decimal? own, decimal? children) => own is > 0 && children > own * 1.25m;

    private BacklogRowDto Build(StackRowDto row, BacklogRowKind kind, int depth, int? rank, IReadOnlyList<RollupCountDto> rollups, decimal? childPoints, bool sumWarn) =>
        new(kind, row.Id, row.Key, row.Type, row.Title, row.State, row.Assignee, row.Points, childPoints, sumWarn, row.Sprint, row.Parent, depth, rank,
            [.. row.Tags.Select(tag => new CardTagDto(tag, _colours.TryGetValue(tag, out var colour) ? colour : null))], rollups, row.Revision);
}
