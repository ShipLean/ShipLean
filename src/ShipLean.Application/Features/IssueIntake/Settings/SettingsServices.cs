using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Settings;

public sealed class SettingsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: the stored project_issue_intake lookup (enabled, lower(repository), github.com only) replaces this, which takes no repository.
        services.AddScoped<IIssueIntakeSettings, NoIssueIntakeSettings>();
    }

    private sealed class NoIssueIntakeSettings : IIssueIntakeSettings
    {
        public Task<IssueIntakeProject?> ForRepositoryAsync(IssueRef issue, CancellationToken cancellationToken = default) => Task.FromResult<IssueIntakeProject?>(null);
    }
}
