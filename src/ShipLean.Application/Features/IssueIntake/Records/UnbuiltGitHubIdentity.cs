using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Records;

// TODO: temporary, so IGitHubIdentity has its one registration before the real GitHubIdentity (GET /user, cached) exists;
// that class replaces this one. Nothing calls it yet: it fails loudly if something does.
public sealed class UnbuiltGitHubIdentity : IGitHubIdentity
{
    public Task<string> SelfLoginAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The GitHub identity is not built yet.");
}
