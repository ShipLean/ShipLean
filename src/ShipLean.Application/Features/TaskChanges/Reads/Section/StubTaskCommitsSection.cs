using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Reads.Section;

public sealed class StubTaskCommitsSection : ITaskCommitsSection
{
    // TODO: read the item's live commits, totals, distinct files and open comment count from the database.
    public Task<TaskCommitsSectionDto> SectionAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult(new TaskCommitsSectionDto(workItemId, [], new TaskCommitTotalsDto(0, 0, 0, 0), [], 0, null));
}
