using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Criteria;

namespace ShipLean.Application.Criteria;

public sealed class CriteriaEvaluatorServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<ICriteriaEvaluator, CriteriaEvaluator>();
}
