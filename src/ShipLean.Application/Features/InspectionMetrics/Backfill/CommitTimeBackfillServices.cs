using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Backfill;

// TODO: temporary; remove once existing installs are backfilled
public sealed class CommitTimeBackfillServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<ICommitTimeBackfill, CommitTimeBackfill>();
}
