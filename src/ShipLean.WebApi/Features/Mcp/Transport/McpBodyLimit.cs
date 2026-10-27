namespace ShipLean.WebApi.Features.Mcp.Transport;

// How large a POST /mcp may be. The gate buffers the whole JSON-RPC message so it can check the tool's scope before the
// tool runs, which is why the body has a hard cap at all. The cap is one attachment at the instance's file limit, written
// as base64 (four characters for every three bytes) plus room for the JSON-RPC envelope, so attach_file can carry a file
// the upload form can. The buffer is the whole request, so a higher SHIPLEAN_ATTACHMENT_MAX_BYTES costs that much memory
// on the call.
public static class McpBodyLimit
{
    // The wrapper, the tool name and the other arguments. Big enough that a file at the limit still reaches the
    // attachment check instead of being refused here.
    public const int EnvelopeBytes = 64 * 1024;

    // Kestrel refuses a body over this before the app reads it, unless the request raises its own limit. The default
    // file limit (25 MB) encodes to more than this, so /mcp does raise it.
    public const long KestrelDefaultMaxBodyBytes = 30_000_000;

    public static long BytesFor(long maxFileBytes)
    {
        var file = maxFileBytes < 0 ? 0 : maxFileBytes;
        return (4 * ((file + 2) / 3)) + EnvelopeBytes;
    }

    public static string TooLarge(long maxBodyBytes)
        => $"The request is larger than {Size(maxBodyBytes)}.";

    // Whole megabytes when the cap is a round number of them, otherwise one decimal, so 1 MB stays "1 MB" and the
    // default cap says how big it actually is.
    public static string Size(long bytes)
    {
        var mb = bytes / (1024d * 1024d);
        return mb >= 1 && bytes % (1024 * 1024) == 0 ? $"{mb:0} MB" : $"{mb:0.#} MB";
    }
}
