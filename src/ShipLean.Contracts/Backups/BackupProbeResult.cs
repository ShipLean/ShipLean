namespace ShipLean.Contracts.Backups;

// What a destination probe found. Step is null when Ok, otherwise the step that failed: WriteStep, ReadStep or
// DeleteStep (the wire values of BackupTestResultDto.step). Message is person-readable and never holds a secret.
public sealed record BackupProbeResult(bool Ok, string? Step, string Message)
{
    public const string WriteStep = "write";
    public const string ReadStep = "read";
    public const string DeleteStep = "delete";
}
