using ShipLean.Contracts.Retros.Analysis;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Analysis;

// TODO: the real export reads the scope's points, retros, commits, open actions and earlier patterns from the database, and replaces this.
internal sealed class FakeRetroExport : IRetroExport
{
    public Task<RetroExportPage> ExportAsync(Guid projectId, DecideScope scope, RetroExportOptions options, CancellationToken cancellationToken)
    {
        var first = options.After is null;
        var point = new RetroExportPoint(
            FakeRetroAnalysisData.PointId, FakeRetroAnalysisData.RetroId, "wentBadly", "The migration was applied before the schema check ran.", "No check ran before apply.",
            "Environment", FakeRetroAnalysisData.Logged, "agent-a", "subAgent",
            new RetroExportItem("SHIP-1", "task", "Add the migration", "Add the tables."),
            new RetroExportFeature("SHIP-9", "Retro analysis", "Turn repeated points into fixes."),
            "Sprint 1",
            [new RetroExportCommit("github.com/ShipLean/ShipLean", "0123456789abcdef0123456789abcdef01234567", "SHIP-1: add the migration", ["db/migrations/202611220900_retros_analysis.sql"])]);
        var retro = new RetroExportRetro("SHIP-1", "Sprint 1", "agent-a", "subAgent", new RetroExportFailureLog(["Schema check missing."], ["Assumed the check ran."], ["Two retries."]));
        IReadOnlyList<RetroExportOpenAction> open = first ? [new RetroExportOpenAction("SHIP-2", "Check the schema before apply", "New", "analyzer", "Environment", 3)] : [];
        IReadOnlyList<RetroExportPattern> previous = first && (options.PatternsFromLatest || options.PatternsFromAnalysis is not null)
            ? [new RetroExportPattern(FakeRetroAnalysisData.PatternId, RetroPatternKind.Problem, "Checks run too late", "Run the check before the step it guards.", "e5-small", [0.1f, 0.2f, 0.3f])]
            : [];
        return Task.FromResult(new RetroExportPage(
            FakeRetroAnalysisData.FeatureScope(), 1, null, [point], new Dictionary<Guid, RetroExportRetro> { [FakeRetroAnalysisData.RetroId] = retro }, open, previous));
    }
}
