using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Fakes;

// TODO: replaced by the real theme reader, which deletes this file and registers its own module.
internal sealed class FakeRetroThemeReader : IRetroThemeReader
{
    public Task<RetroThemeListDto> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new RetroThemeListDto(RetroThemeMode.Pick, [new RetroThemeOptionDto(FakeRetroData.Theme, 1)]));
}

public sealed class FakeRetroThemeReaderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IRetroThemeReader, FakeRetroThemeReader>();
}
