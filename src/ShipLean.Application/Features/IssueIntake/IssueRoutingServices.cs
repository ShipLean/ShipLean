using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake;

public sealed class IssueRoutingServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIssueRouter, IssueRouter>();
    }
}
