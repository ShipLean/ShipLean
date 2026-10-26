namespace ShipLean.WebApi.Tests.Mcp.Planning.Capture;

// How many of the next attachment adds FlakyAttachmentStore fails; a test arms it, each failed add uses one up.
public sealed class AttachFailures
{
    private int _remaining;

    public void Arm(int count) => Interlocked.Exchange(ref _remaining, count);

    public bool TryFail() => Interlocked.Decrement(ref _remaining) >= 0;
}
