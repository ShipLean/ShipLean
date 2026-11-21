namespace ShipLean.Application.Features.Prompts.Updates;

/// <summary>One side's change in base coordinates: base lines [Start, End) are replaced by Lines (End == Start is a pure insertion).</summary>
internal sealed record MergeRegion(int Start, int End, string[] Lines, bool Mine);
