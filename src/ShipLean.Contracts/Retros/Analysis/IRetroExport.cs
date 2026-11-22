using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Contracts.Retros.Analysis;

// What export_retro_points reads. The caller has checked access and resolved the scope; a scope that finds nothing is an empty page.
public interface IRetroExport
{
    Task<RetroExportPage> ExportAsync(Guid projectId, DecideScope scope, RetroExportOptions options, CancellationToken cancellationToken);
}
