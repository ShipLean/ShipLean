namespace ShipLean.Contracts.WorkItems;

// A field a write sets. In WorkItemChanges a null FieldSet means "leave it", and { "value": null } clears a nullable field.
public sealed record FieldSet<T>(T Value);
