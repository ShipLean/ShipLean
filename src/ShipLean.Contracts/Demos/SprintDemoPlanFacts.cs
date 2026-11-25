namespace ShipLean.Contracts.Demos;

// Timing is how long the plan took, read from the Feature's timeline (SHIP-2330); null for data made without it.
public sealed record SprintDemoPlanFacts(int Waves, int Stories, int Tasks, int ReviewRounds, IReadOnlyList<string> PullRequests, SprintDemoTiming? Timing = null);
