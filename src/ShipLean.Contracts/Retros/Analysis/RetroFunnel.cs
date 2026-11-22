using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// Derived by ShipLean from its own data, never taken from the agent. Generic is clustered plus unclustered; ClusteredPercent is clustered over generic.
public sealed record RetroFunnel(
    [property: JsonPropertyName("points_in")] int PointsIn,
    [property: JsonPropertyName("noise")] int Noise,
    [property: JsonPropertyName("folded")] int Folded,
    [property: JsonPropertyName("one_off")] int OneOff,
    [property: JsonPropertyName("generic")] int Generic,
    [property: JsonPropertyName("clustered")] int Clustered,
    [property: JsonPropertyName("clustered_percent")] double ClusteredPercent,
    [property: JsonPropertyName("unclustered")] int Unclustered,
    [property: JsonPropertyName("patterns_problem")] int PatternsProblem,
    [property: JsonPropertyName("patterns_went_well")] int PatternsWentWell,
    [property: JsonPropertyName("fixes_proposed")] int FixesProposed,
    [property: JsonPropertyName("already_enforced_fully")] int AlreadyEnforcedFully,
    [property: JsonPropertyName("already_enforced_partly")] int AlreadyEnforcedPartly,
    [property: JsonPropertyName("discarded")] int Discarded,
    [property: JsonPropertyName("actions_new")] int ActionsNew,
    [property: JsonPropertyName("actions_added")] int ActionsAdded);
