using System.Data.Common;

namespace ShipLean.Contracts.Projects;

// A project's workflow states per work item type. Callers check access first.
public interface IStateCatalog
{
    // Type order (epic, feature, story, bug, task), then state order; the Removed category is included, archived states (deleted in settings) are not. Throws ApiException.NotFound (unknown project).
    Task<IReadOnlyList<StateDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

    // The Agile defaults for every type, with one state per category. Throws ApiException.NotFound (unknown project).
    Task CreateDefaultsAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken = default);

    // Copies every live state of the source; the map is old state id to new state id (for remapping board settings). Throws ApiException.NotFound (unknown project).
    Task<IReadOnlyDictionary<Guid, Guid>> CopyAsync(Guid fromProjectId, Guid toProjectId, DbTransaction transaction, CancellationToken cancellationToken = default);

    // Add (with caller-chosen ids), update (rename and/or recategorise), reorder (may list new states), then remove: a removed state is archived after its items move to the target state, and its name can be reused. After all changes apply every type still needs a Proposed and a Completed state. Throws ApiException.NotFound (unknown project or state), ApiException.Validation (blank or duplicate name, a type left without a Proposed or Completed state, target of another type, reused id).
    Task ApplyAsync(Guid projectId, StateChanges changes, DbTransaction transaction, CancellationToken cancellationToken = default);
}
