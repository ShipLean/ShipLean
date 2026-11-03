using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// What people do to retros in the UI. The access checks run in the controllers; the ids in a body are checked against the route's project here.
internal sealed class RetroPeopleWrites : IRetroPeopleWrites
{
    private readonly RetroPointWriter _points;
    private readonly RetroThemeWriter _themes;

    public RetroPeopleWrites(RetroPointWriter points, RetroThemeWriter themes)
        => (_points, _themes) = (points, themes);

    public Task<RetroPointDto> AddPointAsync(Guid projectId, RetroPointCreate request, CancellationToken cancellationToken = default)
        => _points.AddAsync(projectId, request, cancellationToken);

    public Task<RetroPointDto> MoveThemeAsync(Guid pointId, RetroPointThemePut request, CancellationToken cancellationToken = default)
        => _points.MoveThemeAsync(pointId, request, cancellationToken);

    public Task ArchiveAsync(Guid pointId, CancellationToken cancellationToken = default) => _points.ArchiveAsync(pointId, cancellationToken);

    public Task<RetroThemeRefDto> RenameThemeAsync(Guid themeId, RetroThemeRename request, CancellationToken cancellationToken = default)
        => _themes.RenameAsync(themeId, request, cancellationToken);

    public Task MergeThemesAsync(Guid projectId, RetroThemeMerge request, CancellationToken cancellationToken = default)
        => _themes.MergeAsync(projectId, request, cancellationToken);
}
