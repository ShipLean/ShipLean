namespace ShipLean.Contracts.Persisted;

public sealed record BoardColumnJson(string Id, string Name, int? WipLimit, bool Split, string DefinitionOfDone, IReadOnlyList<ColumnStateMapping> StateMappings);
