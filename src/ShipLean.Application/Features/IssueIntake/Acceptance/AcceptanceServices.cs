using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

public sealed class AcceptanceServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: the real acceptance lane (an accept job that applies the latest proposal and sets the phase last) replaces this no-op.
        services.AddScoped<IIssueAcceptance, NoOpIssueAcceptance>();
    }

    private sealed class NoOpIssueAcceptance : IIssueAcceptance
    {
        public Task<IssueOutcome> AcceptAsync(IssueCommentEvent comment, CancellationToken cancellationToken) => Task.FromResult(IssueOutcome.Ignored);
    }
}
