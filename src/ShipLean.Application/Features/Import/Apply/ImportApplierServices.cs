using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Import.Apply;

// Scoped: the work item store and the sprint store are per request.
public sealed class ImportApplierServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ImportErrorWriter>();
        services.AddScoped<EarlierItems>();
        services.AddScoped<StateProvisioner>();
        services.AddScoped<SprintResolver>();
        services.AddScoped<UserResolver>();
        services.AddScoped<IImportApplier, ImportApplier>();
    }
}
