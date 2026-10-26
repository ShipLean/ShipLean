namespace ShipLean.Contracts.Search;

// Lexical means words only; Reason says why (switched off, model missing, load failed) and is null when nothing needs saying.
public sealed record EmbedderStatus(EmbedderMode Mode, string? Reason = null);
