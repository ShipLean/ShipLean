namespace ShipLean.Application.Features.Backups.Runs;

// Exactly one of the two is set: the lease of the run that started, or why none did.
public sealed record BackupStartResult(BackupLease? Lease, BackupRefusal? Refusal);
