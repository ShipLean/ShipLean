namespace ShipLean.Application.Features.Backups.Cli;

// One encrypted file a verb works on, and for decrypt-backup the path its plaintext is published at.
public sealed record CliFile(string Source, string? Target);
