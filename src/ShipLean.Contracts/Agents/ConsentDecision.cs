using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Agents;

// The person's answer: Allow or deny, and (on allow) the scopes left ticked. Scopes the client did not request are refused.
public sealed record ConsentDecision([property: JsonRequired] bool Allow, IReadOnlyList<string> Scopes);
