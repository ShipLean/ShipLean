using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Analysis;

// TODO: the real writer checks the points against the database, derives the funnel and evidence, creates the actions through the decide planner and stores the analysis, and replaces this.
internal sealed class FakeRetroAnalysisWriter : IRetroAnalysisWriter
{
    public Task<RetroAnalysisOutcome> PreviewAsync(Guid projectId, RecordRetroAnalysisRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Outcome(request, null, []));

    public Task<RetroAnalysisOutcome> ApplyAsync(Guid projectId, RecordRetroAnalysisRequest request, RetroActor actor, CancellationToken cancellationToken)
        => Task.FromResult(Outcome(request, FakeRetroAnalysisData.AnalysisId, ["SHIP-9001"]));

    private static RetroAnalysisOutcome Outcome(RecordRetroAnalysisRequest request, Guid? analysisId, IReadOnlyList<string> created)
    {
        var patterns = request.Patterns.Select((pattern, index) => new RetroRankedPattern(pattern.Ref, pattern.Kind, pattern.Title, index + 1, pattern.Continues, FakeRetroAnalysisData.Evidence())).ToList();
        var funnel = new RetroFunnel(10, 2, 1, 1, 6, 4, 66.7, 2, patterns.Count(p => p.Kind == RetroPatternKind.Problem), patterns.Count(p => p.Kind == RetroPatternKind.WentWell), request.Fixes.Count, 1, 1, 1, 1, 0);
        return new RetroAnalysisOutcome(
            funnel, patterns,
            [new DecideCreateLine("Check the schema before apply", RetroFixType.Analyzer, "Environment", 4, [])],
            [],
            [],
            [],
            analysisId, created);
    }
}
