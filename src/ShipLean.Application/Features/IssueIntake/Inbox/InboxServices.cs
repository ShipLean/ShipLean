using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.IssueIntake.Inbox;

public sealed class InboxServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: the inbox worker (it processes proposal jobs: parse, guard, store, post) is registered here.
    }
}
