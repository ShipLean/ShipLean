namespace ShipLean.LocalHost;

// Either the options or a message for the person who typed the arguments; bad input is not an exception.
public sealed record LocalHostParseResult(LocalHostOptions? Options, string? Error);
