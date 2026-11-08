using ShipLean.Contracts.IssueIntake;

namespace ShipLean.TestSupport.IssueIntake;

// An IGitHubIdentity that answers with a login the test chooses, without calling GitHub.
public sealed class FixedGitHubIdentity(string login = FixedGitHubIdentity.DefaultLogin) : IGitHubIdentity
{
    public const string DefaultLogin = "shiplean-test-bot";

    public string Login { get; set; } = login;

    public Task<string> SelfLoginAsync(CancellationToken cancellationToken = default) => Task.FromResult(Login);
}
