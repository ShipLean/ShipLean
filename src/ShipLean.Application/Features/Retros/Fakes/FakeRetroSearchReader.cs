using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Fakes;

// TODO: replaced by the real search reader, which deletes this file and registers its own module.
internal sealed class FakeRetroSearchReader : IRetroSearchReader
{
    public Task<RetroSearchDto> SearchAsync(Guid projectId, RetroSearchQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(new RetroSearchDto([FakeRetroData.Point()], 1, [FakeRetroData.Sprint], [FakeRetroData.Theme], FakeRetroData.Card()));
}

public sealed class FakeRetroSearchReaderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IRetroSearchReader, FakeRetroSearchReader>();
}
