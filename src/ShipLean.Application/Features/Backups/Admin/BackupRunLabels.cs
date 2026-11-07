using System.Diagnostics;
using System.Globalization;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.Contracts.Refs;
using ShipLean.Core;
using RunState = ShipLean.Application.Features.Backups.Runs.BackupRunState;

namespace ShipLean.Application.Features.Backups.Admin;

// A run log row as the history shows it, with its server labels. DurationLabel is null while running and for an
// abandoned run, whose finish time is when another instance marked it, not when it stopped.
public static class BackupRunLabels
{
    public static BackupRunDto ToDto(BackupRunRow row, UserRef? startedBy, TimeProvider time) => new(
        row.Id,
        row.Trigger switch
        {
            BackupTriggerKind.Scheduled => BackupRunTrigger.Scheduled,
            BackupTriggerKind.Manual => BackupRunTrigger.Manual,
            _ => throw new UnreachableException($"Backup trigger {row.Trigger} has no DTO value."),
        },
        row.State switch
        {
            RunState.Running => Contracts.Backups.Dtos.BackupRunState.Running,
            RunState.Succeeded => Contracts.Backups.Dtos.BackupRunState.Succeeded,
            RunState.Failed => Contracts.Backups.Dtos.BackupRunState.Failed,
            _ => throw new UnreachableException($"A {row.State} backup run is never listed."),
        },
        row.StartedAt,
        DisplayDate.Relative(row.StartedAt, time),
        row.FinishedAt,
        Duration(row),
        startedBy,
        row.PassphraseGeneration,
        row.DatabaseObjectKey,
        row.DatabaseBytes,
        row.AttachmentsCopied,
        row.AttachmentsBytes,
        row.Error,
        row.PrunedAt is not null);

    // "47s", "3m 05s", "1h 02m". Whole seconds, rounded down.
    public static string? Duration(BackupRunRow row)
    {
        if (row.FinishedAt is not { } finished || string.Equals(row.Error, BackupRunLog.AbandonedError, StringComparison.Ordinal))
        {
            return null;
        }
        var total = (long)Math.Max(0, (finished - row.StartedAt).TotalSeconds);
        if (total < 60)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{total}s");
        }
        return total < 3600
            ? string.Create(CultureInfo.InvariantCulture, $"{total / 60}m {total % 60:00}s")
            : string.Create(CultureInfo.InvariantCulture, $"{total / 3600}h {total % 3600 / 60:00}m");
    }
}
