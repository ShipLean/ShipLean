using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.Application.Features.Retros.Analysis;

public sealed class RetroAnalysisReadServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IRetroAnalysisReads, FakeRetroAnalysisReads>();
    }
}
