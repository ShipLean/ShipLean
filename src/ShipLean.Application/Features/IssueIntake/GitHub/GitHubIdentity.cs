using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.GitHub;

// The instance token's own login (GET /user), cached for an hour so a changed token is picked up within that time. A failure
// throws (no token, GitHub unreachable): without the login ShipLean can't tell its own comments apart, so callers retry later.
public sealed class GitHubIdentity(IServiceScopeFactory scopes, TimeProvider clock) : IGitHubIdentity
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    private Cached? _cached;

    public async Task<string> SelfLoginAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        if (_cached is { } cached && now < cached.Until)
        {
            return cached.Login;
        }

        // The client reads the scoped integration settings, so it is resolved in a scope of its own.
        await using var scope = scopes.CreateAsyncScope();
        var login = await scope.ServiceProvider.GetRequiredService<GitHubIssuesClient>().FetchSelfLoginAsync(cancellationToken);
        _cached = new Cached(login, now + CacheFor);
        return login;
    }

    private sealed record Cached(string Login, DateTimeOffset Until);
}
