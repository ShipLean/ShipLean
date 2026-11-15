using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection;

// Options are read when first resolved (so test hosts that override configuration get their own) and a hosted service resolves them at
// startup, so a bad SHIPLEAN_INSPECTION_* value stops the app. ITraceSource is picked by SHIPLEAN_INSPECTION_STORE at resolve time.
// IRunAssembler registers itself (its own attribute).
public sealed class InspectionServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(sp => InspectionOptionsReader.From(sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton<ClickHouseRunFinder>();
        services.AddSingleton<ClickHouseRunReader>();
        services.AddSingleton<ClickHouseHealth>();
        services.AddSingleton<ClickHouseFieldCheck>();
        services.AddSingleton<ClickHouseTraceSource>();
        services.AddSingleton<TempoLokiRunFinder>();
        services.AddSingleton<TempoLokiRunReader>();
        services.AddSingleton<TempoLokiHealth>();
        services.AddSingleton<TempoLokiFieldCheck>();
        services.AddSingleton<TempoLokiTraceSource>();
        services.AddSingleton<DisabledTraceSource>();
        services.AddSingleton<ClickHouseSchemaMigrator>();
        services.AddSingleton<ITraceSource>(sp => sp.GetRequiredService<InspectionOptions>() switch
        {
            { Enabled: true, Store: InspectionOptionsReader.ClickHouse } => sp.GetRequiredService<ClickHouseTraceSource>(),
            { Enabled: true, Store: InspectionOptionsReader.TempoLoki } => sp.GetRequiredService<TempoLokiTraceSource>(),
            _ => sp.GetRequiredService<DisabledTraceSource>(),
        });
        services.AddSingleton<IHostedService>(sp => new InspectionStartup(sp.GetRequiredService<InspectionOptions>(), sp.GetRequiredService<ClickHouseSchemaMigrator>()));
    }

    // Starts the ClickHouse schema migrator only for the ClickHouse store; constructing it is what validated the options.
    private sealed class InspectionStartup(InspectionOptions options, ClickHouseSchemaMigrator migrator) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
            => options is { Enabled: true, Store: InspectionOptionsReader.ClickHouse } ? migrator.StartAsync(cancellationToken) : Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
            => options is { Enabled: true, Store: InspectionOptionsReader.ClickHouse } ? migrator.StopAsync(cancellationToken) : Task.CompletedTask;
    }
}
