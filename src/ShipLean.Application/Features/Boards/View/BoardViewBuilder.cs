using Agoda.IoC.Core;
using ShipLean.Application.Markdown;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Boards.View;

// The read model of a level board. Cards, lanes and columns come from the board's settings and the work items; roll-ups
// come only from IWorkTreeReads. The filter hides cards from Cells but never from the counts.
[RegisterPerRequest]
public sealed class BoardViewBuilder(
    BoardReads reads,
    BoardLaneLayouts layouts,
    IWorkTreeReads tree,
    ICriteriaEvaluator evaluator,
    MarkdownRenderer markdown,
    CardExtraFields extraFields)
{
    // Done shows this many cards per lane, then "Show older" (decision 5).
    public const int DoneShown = 4;

    public async Task<BoardViewDto> BuildAsync(BoardSettingsDto board, BoardViewQuery query, CancellationToken cancellationToken)
    {
        var settings = board.Settings;
        var columnIds = settings.Columns.Select(c => c.Id).ToHashSet();
        var rows = (await reads.CardsAsync(board.ProjectId, BoardLevels.TypesOf(board.Level), null, cancellationToken))
            .Where(r => columnIds.Contains(r.ColumnId!))
            .ToList();
        var epics = new EpicLookup(await reads.HierarchyAsync(board.ProjectId, cancellationToken));
        var layout = await layouts.BuildAsync(board, epics, cancellationToken);
        var rollups = (await tree.RollupsAsync(board.ProjectId, rows.Select(r => r.Id).ToList(), null, cancellationToken)).ToLookup(r => r.ParentId);
        var states = (await reads.StatesAsync([.. settings.Columns.SelectMany(c => c.StateMappings).Select(m => m.StateId).Distinct()], cancellationToken)).ToDictionary(s => s.Id);
        var filter = BoardFilter.From(query);
        var lastColumn = settings.Columns[^1].Id;

        var placed = rows.Select(row =>
        {
            var epicId = epics.EpicOf(row.ParentId);
            return new PlacedCard(row, epicId, layout.LaneOf(row, epicId, evaluator).Id, filter.Shows(row, epicId));
        }).ToList();
        CardDto BuildCard(PlacedCard p) => Card(board, p.Row, p.EpicId, rollups[p.Row.Id].ToList(), p.Row.ColumnId == lastColumn);

        var older = query.Older ?? [];
        return new BoardViewDto(
            board.Id,
            board.ProjectId,
            board.Level,
            placed.Count(p => IsOpen(p.Row)),
            OpenPoints(placed.Select(p => p.Row)),
            [.. settings.Columns.Select(c => Column(c, placed, states))],
            [.. layout.Lanes.Select(l => Lane(l, settings.Columns, placed, lastColumn, older, BuildCard))],
            settings.StyleRules,
            settings.CardFields,
            settings.Annotations);
    }

    private BoardColumnDto Column(BoardColumnJson column, List<PlacedCard> placed, Dictionary<Guid, BoardStateRow> states)
    {
        var cards = placed.Where(p => p.Row.ColumnId == column.Id).ToList();
        var doing = column.Split ? cards.Count(p => !p.Row.ColumnDone) : 0;
        var done = column.Split ? cards.Count(p => p.Row.ColumnDone) : 0;
        return new BoardColumnDto(
            column.Id,
            column.Name,
            column.WipLimit,
            cards.Count,
            cards.Count(p => !p.Visible),
            column.WipLimit is { } limit && cards.Count > limit,
            column.Split,
            doing,
            done,
            column.DefinitionOfDone,
            markdown.Render(column.DefinitionOfDone).Html,
            [.. column.StateMappings.Where(m => states.ContainsKey(m.StateId)).Select(m => new ColumnStateDto(m.Type, State(states[m.StateId])))]);
    }

    private static BoardLaneDto Lane(LaneDef lane, IReadOnlyList<BoardColumnJson> columns, List<PlacedCard> placed, string lastColumn, IReadOnlyList<string> older, Func<PlacedCard, CardDto> buildCard)
    {
        var inLane = placed.Where(p => p.LaneId == lane.Id && p.Visible).ToList();
        var cells = new List<BoardCellDto>();
        foreach (var column in columns)
        {
            foreach (var doneHalf in column.Split ? new[] { false, true } : [false])
            {
                var cards = inLane.Where(p => p.Row.ColumnId == column.Id && (!column.Split || p.Row.ColumnDone == doneHalf));
                cells.Add(column.Id == lastColumn ? DoneCell(lane.Id, column.Id, cards, older, buildCard) : new BoardCellDto(column.Id, doneHalf, [.. cards.OrderBy(p => p.Row.ColumnOrder, StringComparer.Ordinal).ThenBy(p => p.Row.Id).Select(buildCard)], 0, null));
            }
        }

        return new BoardLaneDto(lane.Id, lane.Name, lane.Colour, lane.Kind, lane.CriteriaText, lane.Epic, lane.AcceptsDrops, inLane.Count, OpenPoints(inLane.Select(p => p.Row)), cells);
    }

    // The most recent cards (by when they last changed state) are built; the rest are only counted. "Show older" sends back the
    // cursor to get them all. The cursor names the cell, not a card, so it holds while cards enter or leave Done.
    private static BoardCellDto DoneCell(string laneId, string columnId, IEnumerable<PlacedCard> cards, IReadOnlyList<string> older, Func<PlacedCard, CardDto> buildCard)
    {
        var ordered = cards.OrderByDescending(p => p.Row.StateChangedAt).ThenBy(p => p.Row.Id).ToList();
        if (ordered.Count <= DoneShown)
        {
            return new BoardCellDto(columnId, false, [.. ordered.Select(buildCard)], 0, null);
        }

        var cursor = $"done:{laneId}";
        return older.Contains(cursor)
            ? new BoardCellDto(columnId, false, [.. ordered.Select(buildCard)], 0, null)
            : new BoardCellDto(columnId, false, [.. ordered.Take(DoneShown).Select(buildCard)], ordered.Count - DoneShown, cursor);
    }

    private CardDto Card(BoardSettingsDto board, BoardCardRow row, Guid? epicId, List<WorkTreeRollupDto> below, bool inDone)
    {
        var type = Enum.Parse<WorkItemTypeId>(row.Type, ignoreCase: true);
        var aboveStories = board.Level != BoardLevel.Stories;
        var shown = below.Where(r => !aboveStories || r.Type != WorkItemTypeId.Task).OrderBy(r => r.Type).ToList();
        var childPoints = aboveStories ? ChildPoints(below) : null;
        var values = new CardFieldValues(row, epicId);
        return new CardDto(
            row.Id,
            row.Key,
            TypeRef(type),
            row.Title,
            new StateRef(row.StateId, row.StateName, Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true)),
            row.AssigneeId is { } assignee ? new UserRef(assignee, row.AssigneeName!, row.AssigneeInitials!, row.AssigneeColour!, row.AssigneePhotoUrl) : null,
            row.Points,
            childPoints,
            row.Points is { } points && childPoints is { } sum && sum > points * 1.25m,
            row.ParentId is { } parent ? new ParentRefDto(parent, row.ParentKey!, TypeRef(Enum.Parse<WorkItemTypeId>(row.ParentType!, ignoreCase: true)), row.ParentTitle!) : null,
            [.. row.TagNames.Select(t => new CardTagDto(t, board.Settings.TagColours.FirstOrDefault(c => string.Equals(c.Tag, t, StringComparison.OrdinalIgnoreCase))?.Colour))],
            [.. shown.Select(r => new RollupCountDto(r.Type, r.Open, r.Closed))],
            aboveStories && !inDone && shown.Count > 0 && shown.All(r => r.Open == 0),
            board.Settings.StyleRules.FirstOrDefault(r => r.Enabled && evaluator.Matches(r.Criteria, values))?.Id,
            extraFields.For(row, type, board.Settings.CardFields),
            row.Revision);
    }

    // The points of every Story and Bug below the card, nested Features included; null when none of them has points.
    private static decimal? ChildPoints(List<WorkTreeRollupDto> below)
    {
        var totals = below.Where(r => r.Type is WorkItemTypeId.Story or WorkItemTypeId.Bug && r.TotalPoints is not null).Select(r => r.TotalPoints!.Value).ToList();
        return totals.Count == 0 ? null : totals.Sum();
    }

    private static TypeRef TypeRef(WorkItemTypeId type) => new(type, type == WorkItemTypeId.Story ? "User Story" : type.ToString());

    private static StateRef State(BoardStateRow state) => new(state.Id, state.Name, Enum.Parse<StateCategory>(state.Category, ignoreCase: true));

    private static bool IsOpen(BoardCardRow row) => !string.Equals(row.StateCategory, "completed", StringComparison.OrdinalIgnoreCase);

    private static decimal OpenPoints(IEnumerable<BoardCardRow> rows) => rows.Where(IsOpen).Sum(r => r.Points ?? 0m);

    private sealed record PlacedCard(BoardCardRow Row, Guid? EpicId, string LaneId, bool Visible);
}
