using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Replies;

public sealed class RepliesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: the real replies lane (mirror comments by URL, start a round on a reply from the author or a maintainer) replaces this no-op.
        services.AddScoped<IIssueReplies, NoOpIssueReplies>();
    }

    private sealed class NoOpIssueReplies : IIssueReplies
    {
        public Task<IssueOutcome> ReplyAsync(IssueCommentEvent comment, CancellationToken cancellationToken) => Task.FromResult(IssueOutcome.Ignored);

        public Task<IssueOutcome> MirrorAsync(IssueCommentEvent comment, CancellationToken cancellationToken) => Task.FromResult(IssueOutcome.Ignored);
    }
}
