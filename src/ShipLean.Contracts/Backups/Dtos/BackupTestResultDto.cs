namespace ShipLean.Contracts.Backups.Dtos;

// POST /api/instance/backups/test. A failed probe is Ok false with the step that failed; Step is null when Ok.
public sealed record BackupTestResultDto(bool Ok, BackupTestStep? Step, string Message);
