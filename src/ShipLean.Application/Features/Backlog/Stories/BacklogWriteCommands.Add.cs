using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Backlog.Stories;

public sealed partial class BacklogWriteCommands
{
    // TODO: create the items and their tasks in one transaction, allocate their keys in input order and place them as one block; until then the checks are real and the results are made up.
    public async Task<BacklogAddResult> AddAsync(Guid projectId, BacklogAddRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        RequireWithinLimit(request.Items.Sum(i => 1 + i.Tasks.Count), "rows, counting tasks,");
        var created = request.Items.SelectMany(i => Enumerable.Repeat(0, 1 + i.Tasks.Count)).Select((_, index) => new WriteResult(Guid.NewGuid(), $"NEW-{index + 1}", 1, [], []));
        return new BacklogAddResult([.. created]);
    }

    public async Task<DuplicateCheckResult> DuplicatesAsync(Guid projectId, DuplicateCheckRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        RequireWithinLimit(request.Titles.Count, "titles");
        return new DuplicateCheckResult(await checks.DuplicatesAsync(projectId, request.Titles, cancellationToken));
    }
}
