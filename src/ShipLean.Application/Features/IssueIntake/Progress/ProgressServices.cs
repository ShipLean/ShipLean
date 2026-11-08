using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.IssueIntake.Progress;

public sealed class ProgressServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ProgressReconciler>();
        // HostProfiles removes it in the Test and Local profiles, where tests drive ProgressReconciler.RunPassAsync directly.
        services.AddHostedService<ProgressService>();
    }
}
