using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.WorkItems.Reads.Tree;

public sealed class WorkTreeReadsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IWorkTreeReads, WorkTreeReads>();
}
