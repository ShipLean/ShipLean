using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.WorkItems;

public sealed record RemoveRequest([property: JsonRequired] int ExpectedRevision);
