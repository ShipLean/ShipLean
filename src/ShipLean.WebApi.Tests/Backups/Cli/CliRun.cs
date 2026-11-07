namespace ShipLean.WebApi.Tests.Backups.Cli;

// What one run of a backup verb did: its exit code and what it wrote to standard output and standard error.
public sealed record CliRun(int Code, string Output, string Error);
