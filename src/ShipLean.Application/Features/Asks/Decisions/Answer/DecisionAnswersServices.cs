using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Decisions.Answer;

public sealed class DecisionAnswersServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IDecisionAnswers, DecisionAnswers>();
}
