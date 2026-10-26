using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems.Rules;

public sealed class WorkItemRulesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IWorkItemRules, WorkItemRules>();
}
