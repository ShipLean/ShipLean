using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.WorkItems;

// BeforeItemId null puts the item last in the backlog.
public sealed record ReorderRequest([property: JsonRequired] int ExpectedRevision, Guid? BeforeItemId);
