using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Application.Features.InspectionMetrics.Quality;
using ShipLean.Contracts;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics;

public sealed class InspectionMetricsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInspectionMetrics, InspectionMetricsView>();
        services.AddSingleton<InspectionSprintsReader>();
        services.AddSingleton<ILeadTimeFactsReader, LeadTimeFactsReader>();
        services.AddSingleton<ITaskLeadTimeMeasure, TaskLeadTimeMeasure>();
        services.AddSingleton<ILeadTimeAggregator, LeadTimeAggregator>();
        services.AddSingleton<IQualityFactsReader, QualityFactsReader>();
        services.AddSingleton<IQualityAggregator, QualityAggregator>();
    }
}
