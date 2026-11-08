using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

public sealed class AcceptanceServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIssueAcceptance, DbIssueAcceptance>();
        services.AddSingleton<AcceptanceReads>();
        services.AddSingleton<AcceptJobWorker>();
        services.AddHostedService<AcceptJobService>();
    }
}
