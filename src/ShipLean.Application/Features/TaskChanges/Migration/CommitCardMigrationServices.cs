using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.TaskChanges.Migration;

// Hosted services start in registration order and modules register in name order, after the database module's migration.
public sealed class CommitCardMigrationServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CommitCardRows>();
        services.AddSingleton<CommitCardMigration>();
        services.AddHostedService(provider => provider.GetRequiredService<CommitCardMigration>());
    }
}
