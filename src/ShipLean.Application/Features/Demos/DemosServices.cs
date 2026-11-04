using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Demos.Brand;
using ShipLean.Application.Features.Demos.Data;
using ShipLean.Application.Features.Demos.Prompt;
using ShipLean.Application.Features.Demos.Reads;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Application.Features.Demos.Settings;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos;

// Scoped throughout: the real implementations use the scoped stores and the current user. The YouTube interfaces are registered by the YouTube module.
public sealed class DemosServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISprintDemos, SprintDemos>();
        services.AddScoped<ISprintDemoReader, SprintDemoReader>();
        services.AddScoped<ISprintDemoRuns, SprintDemoRuns>();
        services.AddScoped<ISprintDemoAutomaticRun, SprintDemoAutomaticRun>();
        services.AddScoped<ISprintDemoData, SprintDemoDataReader>();
        services.AddScoped<ISprintDemoPrompt, SprintDemoPrompt>();
        services.AddScoped<IDemoUploads, DemoUploads>();
        services.AddScoped<IDemoDelivery, DemoDelivery>();
        services.AddScoped<IDemoSettings, DemoSettings>();
        services.AddScoped<IDemoBrand, ProjectDemoBrand>();
        services.AddScoped<IDemoLogoStore, DemoLogoStore>();
        services.AddScoped<IDemoTokensStore, DemoTokensStore>();
        services.AddScoped<IDemoBrandSource, DemoBrandSource>();
        services.AddScoped<IDemoLogoUpload, DemoLogoUpload>();
        services.AddScoped<IDemoLogoDownload, DemoLogoDownload>();
        services.AddHostedService<SprintDemoAutomaticSubscriber>();
    }
}
