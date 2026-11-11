namespace ShipLean.Contracts.Asks;

// Post a Markdown comment about a decision; the decision is unchanged.
public sealed record DiscussDecisionRequest(string Body);
