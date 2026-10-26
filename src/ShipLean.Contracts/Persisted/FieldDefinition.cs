using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Persisted;

public sealed record FieldDefinition(string Id, string Label, FieldKind Kind, IReadOnlyList<WorkItemTypeId> AppliesTo, bool Nullable, IReadOnlyList<FieldUse> UsedIn, IReadOnlyList<CriteriaOp> Ops, IReadOnlyList<FieldValueOption>? Values);
