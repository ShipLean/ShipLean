using System.Collections.Concurrent;
using System.Data.Common;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.TestSupport.IssueIntake;

// An IIssueProposalSessions that records each request and answers with Next (Requested unless the test sets it).
public sealed class RecordingProposalSessions : IIssueProposalSessions
{
    private readonly ConcurrentQueue<RecordedRoundRequest> _requests = new();

    public RoundRequest Next { get; set; } = RoundRequest.Requested;

    public IReadOnlyList<RecordedRoundRequest> Requests => [.. _requests];

    public Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        _requests.Enqueue(new RecordedRoundRequest(issueId, force));
        return Task.FromResult(Next);
    }

    public Task LockProjectAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed record RecordedRoundRequest(Guid IssueId, bool Force);
