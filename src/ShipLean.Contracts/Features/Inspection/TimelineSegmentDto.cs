namespace ShipLean.Contracts.Inspection;

// Kind is the row kind as the strip colours it: modelCall, tool, shipleanTool, subagent, event, wait or error.
public sealed record TimelineSegmentDto(string Kind, double StartPct, double EndPct);
