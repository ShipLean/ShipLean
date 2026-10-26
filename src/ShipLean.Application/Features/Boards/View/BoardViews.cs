using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Boards.View;

// What the level board endpoints do: the access check first, then the board's settings, then the read model or the move.
[RegisterPerRequest]
public sealed class BoardViews(IProjectAccess access, IBoardSettingsStore settings, IWorkTreeReads tree, BoardViewBuilder builder, BoardMoves moves)
{
    public async Task<BoardViewDto> GetAsync(Guid boardId, BoardViewQuery query, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Board, boardId, ProjectRole.Member, cancellationToken);
        return await builder.BuildAsync(await LevelBoardAsync(boardId, cancellationToken), query, cancellationToken);
    }

    // A stale expected revision comes back as the store's conflict, unwrapped.
    public async Task<WriteOutcome> MoveAsync(Guid boardId, BoardMoveRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Board, boardId, ProjectRole.Member, cancellationToken);
        return await moves.MoveAsync(await LevelBoardAsync(boardId, cancellationToken), request, cancellationToken);
    }

    // Every descendant of the item at any depth, open first then closed, each with its state; optionally one type only.
    public async Task<IReadOnlyList<ChildSummaryDto>> ChildrenAsync(Guid itemId, WorkItemTypeId? type, CancellationToken cancellationToken)
    {
        var (projectId, _) = await access.RequireAsync(ProjectItem.WorkItem, itemId, ProjectRole.Member, cancellationToken);
        var items = await tree.DescendantsAsync(projectId, itemId, type, cancellationToken);
        return [.. items.Select(i => new ChildSummaryDto(i.Id, i.Key, i.Type, i.Title, i.State, i.Assignee, i.Points))];
    }

    private async Task<BoardSettingsDto> LevelBoardAsync(Guid boardId, CancellationToken cancellationToken)
    {
        var board = await settings.GetAsync(boardId, cancellationToken);
        return board.Level == BoardLevel.Taskboard ? throw ApiException.Validation("The Taskboard has its own view.") : board;
    }
}
