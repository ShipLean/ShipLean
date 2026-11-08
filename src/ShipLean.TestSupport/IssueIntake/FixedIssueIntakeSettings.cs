using ShipLean.Contracts.IssueIntake;

namespace ShipLean.TestSupport.IssueIntake;

// An IIssueIntakeSettings whose projects the test chooses: Take(project) makes it take its Repository's github.com issues,
// compared without case, as the real lookup does.
public sealed class FixedIssueIntakeSettings : IIssueIntakeSettings
{
    private readonly List<IssueIntakeProject> _projects = [];

    public FixedIssueIntakeSettings Take(IssueIntakeProject project)
    {
        _projects.Add(project);
        return this;
    }

    public Task<IssueIntakeProject?> ForRepositoryAsync(IssueRef issue, CancellationToken cancellationToken = default)
    {
        var project = string.Equals(issue.Host, IssueRef.GitHubHost, StringComparison.OrdinalIgnoreCase)
            ? _projects.Find(p => string.Equals(p.Repository, issue.Repository, StringComparison.OrdinalIgnoreCase))
            : null;
        return Task.FromResult(project);
    }
}
