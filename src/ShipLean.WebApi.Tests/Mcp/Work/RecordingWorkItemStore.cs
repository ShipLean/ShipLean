using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Mcp.Work;

// The real store, noting every create: Creates counts single creates, CreateManySizes holds the item count of each batch.
public sealed class RecordingWorkItemStore(IWorkItemStore inner, StoreCreates creates) : DelegatingWorkItemStore(inner)
{
    public override Task<WriteOutcome> CreateAsync(WorkItemCreate create, WriteOptions options, CancellationToken cancellationToken = default)
    {
        creates.NoteCreate();
        return base.CreateAsync(create, options, cancellationToken);
    }

    public override Task<IReadOnlyList<WriteOutcome>> CreateManyAsync(IReadOnlyList<WorkItemCreate> items, WriteOptions options, CancellationToken cancellationToken = default)
    {
        creates.NoteCreateMany(items.Count);
        return base.CreateManyAsync(items, options, cancellationToken);
    }
}
