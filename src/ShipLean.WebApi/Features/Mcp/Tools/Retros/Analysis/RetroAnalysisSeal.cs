namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// What a record_retro_analysis preview seals: only a digest of the arguments, as capture_plan does. The apply recomputes the plan from the arguments.
internal sealed record RetroAnalysisSeal(string Digest);
