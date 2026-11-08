namespace ShipLean.Contracts.IssueIntake;

// The GitHub login of the instance token, so ShipLean can tell its own comments apart (the marker text is not trusted).
public interface IGitHubIdentity
{
    Task<string> SelfLoginAsync(CancellationToken cancellationToken = default);
}
