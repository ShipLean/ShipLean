using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Sprints.Metrics;

public sealed class SprintMetricsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<ISprintMetrics, SprintMetrics>();
}
