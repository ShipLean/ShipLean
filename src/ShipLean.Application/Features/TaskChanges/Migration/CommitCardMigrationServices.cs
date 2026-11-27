using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.TaskChanges.Migration;

// A background service: Kestrel listens at once and the job runs behind it (after the database module's migration, which
// completes in its own StartAsync).
public sealed class CommitCardMigrationServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CommitCardRows>();
        services.AddSingleton<CommitCardMigration>();
        services.AddHostedService(provider => provider.GetRequiredService<CommitCardMigration>());
    }
}
