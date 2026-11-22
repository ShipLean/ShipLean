using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Retros.Analysis.Record;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.Application.Features.Retros.Analysis;

public sealed class RetroAnalysisWriterServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<RetroAnalysisReads>();
        services.AddScoped<RetroAnalysisStore>();
        services.AddScoped<IRetroAnalysisWriter, RetroAnalysisWriter>();
    }
}
