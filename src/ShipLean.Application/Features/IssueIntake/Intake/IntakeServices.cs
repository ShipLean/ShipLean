using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Intake;

public sealed class IntakeServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: the real intake (Triage item, issue record and round on open; edits, closes and reopens; "/shiplean") replaces this no-op.
        services.AddScoped<IIssueIntake, NoOpIssueIntake>();
    }

    private sealed class NoOpIssueIntake : IIssueIntake
    {
        public Task<IssueOutcome> OpenedAsync(IssueEvent issue, CancellationToken cancellationToken) => Task.FromResult(IssueOutcome.Ignored);

        public Task<IssueOutcome> ChangedAsync(IssueEvent issue, CancellationToken cancellationToken) => Task.FromResult(IssueOutcome.Ignored);

        public Task<IssueOutcome> TakeAsync(IssueCommentEvent comment, CancellationToken cancellationToken) => Task.FromResult(IssueOutcome.Ignored);
    }
}
