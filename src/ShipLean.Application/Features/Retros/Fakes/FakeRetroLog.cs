using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Fakes;

// TODO: replaced by the real retro log, which deletes this file and registers its own module.
// There is no not_implemented error code; not_configured (503) is the nearest one that exists.
internal sealed class FakeRetroLog : IRetroLog
{
    public Task<RetroLogResult> LogAsync(RetroLogRequest request, CancellationToken cancellationToken = default)
        => throw ApiException.NotConfigured("not_implemented: logging a retrospective is not built yet.");

    public Task<RetroListResult> ListAsync(RetroListRequest request, CancellationToken cancellationToken = default)
        => throw ApiException.NotConfigured("not_implemented: listing retrospectives is not built yet.");
}

public sealed class FakeRetroLogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IRetroLog, FakeRetroLog>();
}
