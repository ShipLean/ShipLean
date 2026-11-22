using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.Application.Features.Retros.Analysis;

// The fixed rows the three fake services share, in the final shape. Replaced with the real services' reads.
internal static class FakeRetroAnalysisData
{
    public static readonly Guid PointId = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    public static readonly Guid RetroId = Guid.Parse("b0000000-0000-4000-8000-000000000001");
    public static readonly Guid PatternId = Guid.Parse("c0000000-0000-4000-8000-000000000001");
    public static readonly Guid AnalysisId = Guid.Parse("d0000000-0000-4000-8000-000000000001");

    public static readonly DateTimeOffset Logged = DateTimeOffset.Parse("2026-11-20T09:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);

    public static RetroEvidence Evidence() => new(2, ["SHIP-1"], ["SHIP-9"], ["Sprint 1"], ["agent-a"], Logged, Logged.AddDays(1));

    public static RetroScopeView FeatureScope() => new(RetroScopeKind.Feature, "SHIP-9", null, null, null);
}
