using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.Application.Features.Backups.Schedule;

// A run the trigger started and the worker runs: its lease (owned by the worker from here on) and the config snapshot the
// trigger read for it.
public sealed record BackupRunRequest(BackupLease Lease, BackupConfig Snapshot);
