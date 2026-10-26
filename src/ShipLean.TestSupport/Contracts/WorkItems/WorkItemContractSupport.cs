using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.WorkItems;

// Helpers the work item contract fixtures share.
public static class WorkItemContractSupport
{
    public static Guid Actor => SeedIds.Users.Joel;

    public static WriteResult Written(WriteOutcome outcome)
    {
        outcome.Conflict.ShouldBeNull();
        return outcome.Written.ShouldNotBeNull();
    }

    public static async Task<WriteResult> CreateAsync(
        IWorkItemStore store, ArrangedProject project, WorkItemTypeId type, string title, Guid? parentId = null)
        => Written(await store.CreateAsync(new WorkItemCreate(project.ProjectId, type, title, parentId, null, null), WriteOptions.User));
}
