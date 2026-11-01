using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Rules;

// AfterAvg is null when the verdict is TooEarly. ChangePct is a fraction, (after - before) / before, and null when before is 0 or after is null.
public sealed record RetroVerdictResult(RetroVerdict Verdict, double BeforeAvg, double? AfterAvg, double? ChangePct);
