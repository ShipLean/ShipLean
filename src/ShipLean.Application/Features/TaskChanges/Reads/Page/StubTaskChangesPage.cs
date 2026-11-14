using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Reads.Page;

public sealed class StubTaskChangesPage : ITaskChangesPage
{
    // TODO: read the item, its commits, the selected diff parts and the code comments.
    public Task<TaskChangesDto> PageAsync(Guid workItemId, string commit, CancellationToken cancellationToken = default)
        => Task.FromResult(new TaskChangesDto(workItemId, string.Empty, string.Empty, "task", "proposed", [], commit, "combined", null, [], [], 0));
}
