namespace ShipLean.TestSupport.YouTube;

// One request the stub received. Body is set for JSON and form requests; Authorization is the header as sent.
public sealed record TestYouTubeCall(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    long BodyBytes,
    int BodyReads,
    long LargestRead);
