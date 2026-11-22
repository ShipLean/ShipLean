using ShipLean.Contracts.Retros;

namespace ShipLean.Contracts.Retros.Analysis;

// What record_retro_analysis does. Preview writes nothing. Apply runs the actions through IRetroDecidePlanner first (each creates its own work item), then
// stores the analysis in one transaction; it is not one transaction with the actions. The caller has checked access and resolved the scope.
public interface IRetroAnalysisWriter
{
    Task<RetroAnalysisOutcome> PreviewAsync(Guid projectId, RecordRetroAnalysisRequest request, CancellationToken cancellationToken);

    Task<RetroAnalysisOutcome> ApplyAsync(Guid projectId, RecordRetroAnalysisRequest request, RetroActor actor, CancellationToken cancellationToken);
}
