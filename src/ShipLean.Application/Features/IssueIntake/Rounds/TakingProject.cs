using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// The intake settings of the project that still takes an issue's repository: null when no project takes it now, or another
// project does. IIssueIntakeSettings is scoped, so it is read in a scope of its own.
internal static class TakingProject
{
    public static async Task<IssueIntakeProject?> FindAsync(IServiceScopeFactory scopes, GitHubIssueRecord issue, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var project = await scope.ServiceProvider.GetRequiredService<IIssueIntakeSettings>().ForRepositoryAsync(issue.Issue, cancellationToken);
        return project?.ProjectId == issue.ProjectId ? project : null;
    }
}
