namespace ShipLean.Contracts.Inbox;

// The Inbox's fixed rules. StatusStaleAfter is the Inbox's own "Still true?" age; the web's Verified chip keeps its own.
public static class InboxRules
{
    public static readonly TimeSpan StatusStaleAfter = TimeSpan.FromDays(3);

    public static readonly TimeSpan LongWaitAfter = TimeSpan.FromDays(2);

    public static readonly TimeSpan AnsweredWindow = TimeSpan.FromDays(7);

    public const int AnsweredLimit = 50;

    public const int MaxQuestionOptions = 6;

    public const int ExcerptLength = 280;
}
