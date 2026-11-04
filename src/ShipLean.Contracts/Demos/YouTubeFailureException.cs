namespace ShipLean.Contracts.Demos;

// A YouTube call failed in a way a person can act on. The message is person-readable and never carries a token or a code.
public sealed class YouTubeFailureException(YouTubeFailureKind kind, string message) : Exception(message)
{
    public YouTubeFailureKind Kind { get; } = kind;
}
