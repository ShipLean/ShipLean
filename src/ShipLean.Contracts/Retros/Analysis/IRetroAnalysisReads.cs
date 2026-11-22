namespace ShipLean.Contracts.Retros.Analysis;

// What get_retro_analysis reads. GetAsync is null when the project has no live analysis with that id; ListAsync is the project's newest 10, newest first.
public interface IRetroAnalysisReads
{
    Task<RetroAnalysisView?> GetAsync(Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RetroAnalysisRow>> ListAsync(Guid projectId, CancellationToken cancellationToken);
}
