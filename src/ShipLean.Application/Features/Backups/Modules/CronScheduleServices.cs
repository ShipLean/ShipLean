using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Cron;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

// The backup schedule's cron parser and checks.
public sealed class CronScheduleServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<ICronSchedule, CronSchedule>();
}
