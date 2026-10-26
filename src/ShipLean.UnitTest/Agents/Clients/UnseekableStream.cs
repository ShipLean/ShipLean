namespace ShipLean.UnitTest.Agents.Clients;

// A body whose length is not known up front, as a chunked response's is.
internal sealed class UnseekableStream(byte[] bytes) : MemoryStream(bytes)
{
    public override bool CanSeek => false;
}
