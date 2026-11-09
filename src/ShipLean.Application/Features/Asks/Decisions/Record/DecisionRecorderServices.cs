using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Decisions.Record;

public sealed class DecisionRecorderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IDecisionRecorder, DecisionRecorder>();
}
