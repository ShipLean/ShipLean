namespace ShipLean.Contracts.IssueIntake;

// What one GitHub write did, as github_outbox.last_result records it. Ok: done (CommentId for a new comment, null otherwise).
// Retry: not done, try again later (429, 5xx, a rate-limited 403, a timeout or a connection that never opened, except where a
// POST is Ambiguous). Ambiguous: a POST that GitHub may have carried out (a timeout or a 5xx after the body was sent). Failed:
// will not work by trying again (401, 403, 404, 410, 422, no token). Error is a short code or status, never a token or body.
public abstract record GitHubWriteResult
{
    private GitHubWriteResult()
    {
    }

    public sealed record Ok(long? CommentId) : GitHubWriteResult;

    public sealed record Retry(string Error) : GitHubWriteResult;

    public sealed record Ambiguous(string Error) : GitHubWriteResult;

    public sealed record Failed(string Error) : GitHubWriteResult;
}
