using Microsoft.Extensions.Hosting;

namespace ShipLean.Application.Features.Inspection.ClickHouse.Schema;

public sealed class ClickHouseSchemaMigrator : IHostedService
{
    // TODO: create the inspection database and the views over the exporter's tables, through QueryText for configured names.
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
