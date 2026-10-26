using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Boards.View;

// A card dropped on a level board: the column, half and lane go to the work item store's move, together with the field
// writes the lane implies (a criteria lane's values, the default when leaving one, or the Epic a Feature joins), so they
// share the one revision.
[RegisterPerRequest]
public sealed class BoardMoves(BoardReads reads, BoardLaneLayouts laneLayouts, ICriteriaEvaluator evaluator, ICurrentUser currentUser, IWorkItemStore store)
{
    public async Task<WriteOutcome> MoveAsync(BoardSettingsDto board, BoardMoveRequest request, CancellationToken cancellationToken)
    {
        var types = BoardLevels.TypesOf(board.Level);
        var card = (await reads.CardsAsync(board.ProjectId, BoardLevels.AllTypes, request.ItemId, cancellationToken)).SingleOrDefault()
            ?? throw ApiException.NotFound("That card isn't on this project.");
        if (!types.Contains(card.Type))
        {
            throw ApiException.Validation($"A {card.Type} isn't on this board.");
        }

        if (card.Revision != request.ExpectedRevision)
        {
            // The store answers a stale write with its own conflict, whatever the lane says.
            return await store.MoveAsync(card.Id, new MoveTarget(request.ExpectedRevision, request.ColumnId, request.Done, null, request.BeforeItemId, null), WriteOptions.User, cancellationToken);
        }

        var epics = new EpicLookup(await reads.HierarchyAsync(board.ProjectId, cancellationToken));
        var layout = await laneLayouts.BuildAsync(board, epics, cancellationToken);
        var (laneId, set) = Lane(board.Level, layout, epics, card, request.LaneId);
        var target = new MoveTarget(request.ExpectedRevision, request.ColumnId, request.Done, laneId, request.BeforeItemId, set);
        return await store.MoveAsync(card.Id, target, WriteOptions.User, cancellationToken);
    }

    // The stored lane id to write and the field changes the drop into the requested lane carries. A generated Epic lane is
    // recognised by its id, whatever the user's lane preference says now. On a board that shows one lane the request's lane is ignored.
    private (string? LaneId, WorkItemChanges? Set) Lane(BoardLevel level, LaneLayout layout, EpicLookup epics, BoardCardRow card, string? requested)
    {
        var epicId = epics.EpicOf(card.ParentId);
        if (requested is null)
        {
            return (null, null);
        }

        if (level == BoardLevel.Features && (requested == LaneLayout.NoEpicLaneId || requested.StartsWith(LaneLayout.EpicLanePrefix, StringComparison.Ordinal)))
        {
            return (null, EpicDrop(BoardLaneLayouts.ByEpic(epics), card, epicId, requested));
        }

        return layout.Mode == LaneMode.Stored ? StoredDrop(layout, card, epicId, requested) : (null, null);
    }

    // A Feature dropped into another Epic's lane becomes that Epic's direct child; No Epic clears the parent. Dropping into the
    // lane the Feature already sits in changes nothing (a nested Feature keeps its Feature parent).
    private WorkItemChanges? EpicDrop(LaneLayout layout, BoardCardRow card, Guid? epicId, string requested)
    {
        var lane = layout.Find(requested) ?? throw ApiException.Validation("That lane isn't on the board.");
        if (lane.Id == layout.LaneOf(card, epicId, evaluator).Id)
        {
            return null;
        }

        return new WorkItemChanges { Parent = new FieldSet<Guid?>(lane.Epic?.Id) };
    }

    private (string? LaneId, WorkItemChanges? Set) StoredDrop(LaneLayout layout, BoardCardRow card, Guid? epicId, string requested)
    {
        var lane = layout.Find(requested) ?? throw ApiException.Validation("That lane isn't on the board.");
        if (lane.Id == layout.LaneOf(card, epicId, evaluator).Id)
        {
            return (lane.Id, null);
        }

        var matched = layout.MatchedCriteriaLanes(card, epicId, evaluator).Select(l => l.Criteria!).ToList();
        var leaving = matched.Count == 0 ? null : LaneWrites.Leaving(matched);
        if (lane.Kind == BoardLaneKind.Criteria)
        {
            if (lane.Criteria is not { Clauses.Count: > 0 })
            {
                throw ApiException.Validation("This lane has no criteria, so cards can't be dropped into it.");
            }

            return lane.AcceptsDrops
                ? (lane.Id, LaneWrites.Entering(lane.Criteria, currentUser.Id, leaving))
                : throw ApiException.Validation($"Cards can't be dropped into {lane.Name}: its criteria aren't all \"=\" on a field a drop can set.");
        }

        return (lane.Id, leaving);
    }
}
