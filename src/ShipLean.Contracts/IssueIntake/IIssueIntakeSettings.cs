namespace ShipLean.Contracts.IssueIntake;

// Which project takes a GitHub repository's issues (D5): the one whose intake is on for that repository, compared without
// case. github.com only; any other host, or a repository no project takes, is null.
public interface IIssueIntakeSettings
{
    Task<IssueIntakeProject?> ForRepositoryAsync(IssueRef issue, CancellationToken cancellationToken = default);
}
