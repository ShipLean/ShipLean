namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoPlanFacts(int Waves, int Stories, int Tasks, int ReviewRounds, IReadOnlyList<string> PullRequests);
