using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.WorkItems;

public sealed record StatusConfirmRequest([property: JsonRequired] int Revision);
