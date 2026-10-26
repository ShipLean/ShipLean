using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Mapping;

// Scoped: the mapping reads saved jobs through IImportJobs, which the job runner makes per request.
public sealed class ImportMappingsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IImportMappings, ImportMappings>();
}
