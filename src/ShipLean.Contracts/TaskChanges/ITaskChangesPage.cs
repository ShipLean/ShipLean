using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Contracts.TaskChanges;

// The read behind the Changes page. Commit is "all" or a work_item_commit id. Callers check IProjectAccess first; this trusts them.
public interface ITaskChangesPage
{
    Task<TaskChangesDto> PageAsync(Guid workItemId, string commit, CancellationToken cancellationToken = default);
}
