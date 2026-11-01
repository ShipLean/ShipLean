using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Fakes;

// TODO: replaced by the real work item retro reader, which deletes this file and registers its own module.
internal sealed class FakeWorkItemRetroReader : IWorkItemRetroReader
{
    public Task<WorkItemRetrosDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult(new WorkItemRetrosDto([FakeRetroData.Card()]));
}

public sealed class FakeWorkItemRetroReaderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IWorkItemRetroReader, FakeWorkItemRetroReader>();
}
