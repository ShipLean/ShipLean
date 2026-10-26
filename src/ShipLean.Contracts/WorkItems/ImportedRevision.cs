using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.WorkItems;

// One entry of the source system's changelog, already mapped to field changes. ChangedBy is the user the importer
// resolved, or the seeded Jira import user.
public sealed record ImportedRevision(Guid ChangedBy, DateTimeOffset ChangedAt, IReadOnlyList<FieldChange> Changes);
