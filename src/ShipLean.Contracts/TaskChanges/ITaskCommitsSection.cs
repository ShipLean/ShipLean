using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Contracts.TaskChanges;

// The read behind the Task page's Changes section: the item's live commits, their totals, the distinct files and the open comment count.
// Callers check IProjectAccess first; this trusts them.
public interface ITaskCommitsSection
{
    Task<TaskCommitsSectionDto> SectionAsync(Guid workItemId, CancellationToken cancellationToken = default);
}
