namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// What the acceptance lane posts on GitHub and writes on the Triage item. Only keys, titles and fixed text (D17); every GitHub
// text still passes PublicTextGuard before it is enqueued.
public static class AcceptanceTexts
{
    public const string Busy = "A new proposal is being written; reply LGTM after it.";

    public const string QuestionsOpen = "The proposal's questions are still open. Please answer them in a reply, and a new proposal will follow.";

    public const string Shipped = "Thanks for confirming. This has already shipped, so this issue is closed as completed.";

    public const string AlreadyShippedNote = "Already shipped";

    public const string UnsafeReason = "unsafeText";

    public static string Accepted(string key, string title)
        => $"Thanks! This is now {key} in the backlog: {title}. This issue stays open, and gets an update at each step until the work is done.";

    public static string Duplicate(string key, string title)
        => $"Thanks! This is the same as {key}: {title}. This issue stays open, and gets an update at each step until that work is done.";

    public static string Became(string key) => $"Became {key}";

    public static string CameFrom(string key) => $"Came from {key}";

    public static string DuplicateOf(string key) => $"Duplicate of {key}";

    public static string Unsafe(string reason) => $"Held for a maintainer: the acceptance reply did not pass the public text check ({reason}). Nothing was accepted.";

    // The ShipLean note on the item when an accept job failed for good and the issue went back to waiting for a reply.
    public const string Failed = "Accepting the proposal failed, and ShipLean stopped trying. Nothing more was accepted. A maintainer can retry: reply LGTM on the GitHub issue, or comment /shiplean there.";

    public static string BusyKey(long commentId) => $"busy:{commentId}";

    public static string QuestionsKey(long commentId) => $"needs-info:{commentId}";

    // The outbox entry that posts a proposal again after the issue is reopened (proposal:<session> is the first post).
    public static string ReopenedProposalKey(Guid sessionId) => $"proposal:{sessionId}:reopen";

    public static string JobKey(Guid sessionId) => $"accept:{sessionId}";

    public static string CreateKey(Guid sessionId) => $"accept-create:{sessionId}";

    public static string PatchKey(Guid sessionId) => $"accept-patch:{sessionId}";

    public static string NoteKey(Guid sessionId, Guid itemId) => $"accept-note:{sessionId}:{itemId}";

    public static string ReplyKey(Guid sessionId) => $"accepted:{sessionId}";

    public static string CloseKey(Guid sessionId) => $"accept-close:{sessionId}";
}
