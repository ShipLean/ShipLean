using System.Collections.Concurrent;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.TestSupport.IssueIntake;

// One double for the three issue lanes: it records each call as (lane method, event) and answers Processed with Item, so a
// routing test can see which lane got the event. Register the one instance for IIssueIntake, IIssueReplies and
// IIssueAcceptance.
public sealed class RecordingIssueHandlers : IIssueIntake, IIssueReplies, IIssueAcceptance
{
    private readonly ConcurrentQueue<IssueCall> _calls = new();

    public string? Item { get; set; } = "SHIP-1";

    public IReadOnlyList<IssueCall> Calls => [.. _calls];

    public Task<IssueOutcome> OpenedAsync(IssueEvent issue, CancellationToken cancellationToken) => Record(nameof(OpenedAsync), issue);

    public Task<IssueOutcome> ChangedAsync(IssueEvent issue, CancellationToken cancellationToken) => Record(nameof(ChangedAsync), issue);

    public Task<IssueOutcome> TakeAsync(IssueCommentEvent comment, CancellationToken cancellationToken) => Record(nameof(TakeAsync), comment);

    public Task<IssueOutcome> ReplyAsync(IssueCommentEvent comment, CancellationToken cancellationToken) => Record(nameof(ReplyAsync), comment);

    public Task<IssueOutcome> MirrorAsync(IssueCommentEvent comment, CancellationToken cancellationToken) => Record(nameof(MirrorAsync), comment);

    public Task<IssueOutcome> AcceptAsync(IssueCommentEvent comment, CancellationToken cancellationToken) => Record(nameof(AcceptAsync), comment);

    private Task<IssueOutcome> Record(string method, object received)
    {
        _calls.Enqueue(new IssueCall(method, received));
        return Task.FromResult(IssueOutcome.Processed(Item));
    }

    // One call: the lane method's name and the IssueEvent or IssueCommentEvent it was given.
    public sealed record IssueCall(string Method, object Event);
}
