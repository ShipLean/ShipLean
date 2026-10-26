using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.WorkItems;

// Revision is the item's revision after the write. Changes is what the write recorded (empty when nothing changed and
// no revision was written). Notices are rule messages such as "pulled".
public sealed record WriteResult(Guid Id, string Key, int Revision, IReadOnlyList<FieldChange> Changes, IReadOnlyList<string> Notices);
