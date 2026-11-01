using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Fakes;

// TODO: replaced by the real people writes, which deletes this file and registers its own module.
internal sealed class FakeRetroPeopleWrites : IRetroPeopleWrites
{
    public Task<RetroPointDto> AddPointAsync(Guid projectId, RetroPointCreate request, CancellationToken cancellationToken = default)
        => Task.FromResult(FakeRetroData.Point());

    public Task<RetroPointDto> MoveThemeAsync(Guid pointId, RetroPointThemePut request, CancellationToken cancellationToken = default)
        => Task.FromResult(FakeRetroData.Point() with { Id = pointId });

    public Task ArchiveAsync(Guid pointId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<RetroActionRefDto> CreateActionAsync(Guid pointId, RetroActionCreate request, CancellationToken cancellationToken = default)
        => Task.FromResult(FakeRetroData.Action);

    public Task<RetroThemeRefDto> RenameThemeAsync(Guid themeId, RetroThemeRename request, CancellationToken cancellationToken = default)
        => Task.FromResult(FakeRetroData.Theme with { Id = themeId, Name = request.Name });

    public Task MergeThemesAsync(Guid projectId, RetroThemeMerge request, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class FakeRetroPeopleWritesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IRetroPeopleWrites, FakeRetroPeopleWrites>();
}
