namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// What a capture_plan preview seals into its plan token: the digest of the arguments it checked, the existing Feature it
// checked them against (null for a new one) and the id of this plan, which the apply claims so that a preview applies at most
// once. The tree itself is not sealed: the apply builds it from its own arguments.
public sealed record CapturePlanSeal(string Digest, Guid? FeatureId, Guid PlanId);
