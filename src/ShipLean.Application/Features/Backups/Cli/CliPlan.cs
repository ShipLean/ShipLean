namespace ShipLean.Application.Features.Backups.Cli;

// The files a verb will work on, or the usage problem that stops it before anything is read or written.
public sealed record CliPlan(IReadOnlyList<CliFile> Files, string? Problem)
{
    public static CliPlan Refuse(string problem) => new([], problem);
}
