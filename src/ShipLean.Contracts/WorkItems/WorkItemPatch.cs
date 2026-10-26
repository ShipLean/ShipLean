using System.Text.Json.Serialization;

namespace ShipLean.Contracts.WorkItems;

public sealed record WorkItemPatch([property: JsonRequired] int ExpectedRevision, WorkItemChanges Changes);
