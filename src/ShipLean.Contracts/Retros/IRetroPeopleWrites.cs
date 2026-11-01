namespace ShipLean.Contracts.Retros;

// What people do to retros in the UI. Callers have checked the shape of the body (RetroChecks) and the access to the route's project, point or theme only.
// The ids inside a body (RetroPointCreate.RetroId/WorkItemId/SprintId, RetroThemeMerge.SourceIds/TargetId) are not checked by callers:
// the implementation must answer not_found for any of them outside projectId.
public interface IRetroPeopleWrites
{
    Task<RetroPointDto> AddPointAsync(Guid projectId, RetroPointCreate request, CancellationToken cancellationToken = default);

    Task<RetroPointDto> MoveThemeAsync(Guid pointId, RetroPointThemePut request, CancellationToken cancellationToken = default);

    // 400 validation for an agent-written point.
    Task ArchiveAsync(Guid pointId, CancellationToken cancellationToken = default);

    // 400 validation unless the point's category is improve; 409 if it already has an action item.
    Task<RetroActionRefDto> CreateActionAsync(Guid pointId, RetroActionCreate request, CancellationToken cancellationToken = default);

    Task<RetroThemeRefDto> RenameThemeAsync(Guid themeId, RetroThemeRename request, CancellationToken cancellationToken = default);

    Task MergeThemesAsync(Guid projectId, RetroThemeMerge request, CancellationToken cancellationToken = default);
}
