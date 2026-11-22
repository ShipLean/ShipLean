using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.Application.Features.Retros.Analysis.Record;

// The C2 ranking (D11): tier fix > structure > analyzer > test > skill = guide > product > other, then the pattern's point count, then the latest day one was logged, then
// the distinct items it spans; ties keep the order the agent sent. A pattern is ranked by its top acting fix; a problem pattern with none follows those that have one, and
// a wentWell pattern comes last. A fix's rank is its place among its own pattern's fixes by tier. Acting means notCaught or partly: fully and discarded never make an action.
internal static class RetroAnalysisRanking
{
    internal sealed record RankedFix(RetroAnalysisFix Fix, int Rank, bool Acting);

    internal sealed record RankedPattern(RetroAnalysisPattern Pattern, int Rank, RetroEvidence Evidence, IReadOnlyList<RankedFix> Fixes)
    {
        // The top-ranked acting fix: the one that becomes the pattern's action item.
        public RankedFix? Action => Fixes.FirstOrDefault(fix => fix.Acting);
    }

    public static int Tier(RetroFixType type)
        => type switch
        {
            RetroFixType.Fix => 0,
            RetroFixType.Structure => 1,
            RetroFixType.Analyzer => 2,
            RetroFixType.Test => 3,
            RetroFixType.Skill or RetroFixType.Guide => 4,
            RetroFixType.Product => 5,
            _ => 6,
        };

    public static bool IsActing(RetroEnforcement enforcement) => enforcement is RetroEnforcement.NotCaught or RetroEnforcement.Partly;

    public static IReadOnlyList<RankedPattern> RankPatterns(RecordRetroAnalysisRequest request, IReadOnlyDictionary<string, RetroEvidence> evidence)
    {
        var unranked = request.Patterns.Select(pattern => (Pattern: pattern, Fixes: RankFixes(request.Fixes.Where(fix => fix.PatternRef == pattern.Ref)))).ToList();
        var ordered = unranked
            .OrderBy(entry => Group(entry.Pattern, entry.Fixes))
            .ThenBy(entry => entry.Fixes.FirstOrDefault(fix => fix.Acting) is { } top ? Tier(top.Fix.FixType) : 0)
            .ThenByDescending(entry => evidence[entry.Pattern.Ref].Points)
            .ThenByDescending(entry => evidence[entry.Pattern.Ref].LastLogged)
            .ThenByDescending(entry => evidence[entry.Pattern.Ref].Items.Count)
            .ToList();
        return [.. ordered.Select((entry, index) => new RankedPattern(entry.Pattern, index + 1, evidence[entry.Pattern.Ref], entry.Fixes))];
    }

    private static List<RankedFix> RankFixes(IEnumerable<RetroAnalysisFix> fixes)
        => [.. fixes.OrderBy(fix => Tier(fix.FixType)).Select((fix, index) => new RankedFix(fix, index + 1, IsActing(fix.Enforcement)))];

    // 0: a problem pattern with an acting fix; 1: a problem pattern with none; 2: a wentWell pattern.
    private static int Group(RetroAnalysisPattern pattern, List<RankedFix> fixes)
    {
        if (pattern.Kind == RetroPatternKind.WentWell)
        {
            return 2;
        }

        return fixes.Any(fix => fix.Acting) ? 0 : 1;
    }
}
