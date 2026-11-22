using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.Application.Features.Retros.Analysis;

// TODO: the real reads load the stored analyses from the database, and replace this. The fake answers any id with the same analysis under that id.
internal sealed class FakeRetroAnalysisReads : IRetroAnalysisReads
{
    public Task<RetroAnalysisView?> GetAsync(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var action = new RetroViewAction("SHIP-2", "New");
        var fix = new RetroViewFix("Check the schema before apply", "analyzer", RetroEnforcement.NotCaught, null, "Nothing checks the schema before apply.", action);
        var also = new RetroViewFix("Say it in the guide", "guide", RetroEnforcement.Partly, "The planning guide", "The guide mentions it once.", action);
        var thrown = new RetroViewFix("Add a lint rule", "analyzer", RetroEnforcement.Discarded, null, "The rule only sounds right.", null);
        var problem = new RetroViewPattern(FakeRetroAnalysisData.PatternId, "p1", RetroPatternKind.Problem, "Checks run too late", "Run the check before the step it guards.", 1, null, FakeRetroAnalysisData.Evidence(), [fix, also]);
        var wentWell = new RetroViewPattern(Guid.Parse("c0000000-0000-4000-8000-000000000002"), "w1", RetroPatternKind.WentWell, "Small commits", "Commit small and often.", 1, null, FakeRetroAnalysisData.Evidence(), []);
        var funnel = new RetroFunnel(10, 2, 1, 1, 6, 4, 66.7, 2, 1, 1, 3, 0, 1, 1, 1, 0);
        return Task.FromResult<RetroAnalysisView?>(new RetroAnalysisView(
            id, FakeRetroAnalysisData.FeatureScope(), FakeRetroAnalysisData.Logged, "agent-a", "e5-small", "claude-sonnet", funnel, [problem], [wentWell],
            new Dictionary<string, IReadOnlyList<RetroViewFix>> { ["analyzer"] = [fix], ["guide"] = [also] }, [thrown], "# Report\n\nOne pattern, one action."));
    }

    public Task<IReadOnlyList<RetroAnalysisRow>> ListAsync(Guid projectId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RetroAnalysisRow>>([new RetroAnalysisRow(FakeRetroAnalysisData.AnalysisId, FakeRetroAnalysisData.FeatureScope(), FakeRetroAnalysisData.Logged, "agent-a", 2, 1)]);
}
