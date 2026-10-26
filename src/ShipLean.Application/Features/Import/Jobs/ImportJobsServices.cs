using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Jobs;

// Scoped, like the applier and the stores a retry reaches. The runner is the one hosted service that works through the queue.
public sealed class ImportJobsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IImportJobs, ImportJobs>();
        services.AddHostedService<ImportRunner>();
    }
}
