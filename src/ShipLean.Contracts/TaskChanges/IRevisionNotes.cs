using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.TaskChanges;

// Writes a revision that changes no column: one FieldChange whose ToLabel is the history line ("Commits +3 (acme/storefront)").
public interface IRevisionNotes
{
    Task AddAsync(Guid workItemId, string field, string toLabel, WriteOptions options, CancellationToken cancellationToken = default);
}
