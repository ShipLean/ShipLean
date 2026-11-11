namespace ShipLean.WebApi.Features.Mcp.Payloads;

// A tool that takes a payload, and the names of its string arguments a payload may fill.
public sealed record PayloadTarget(string Tool, IReadOnlySet<string> TextFields);
