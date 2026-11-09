using System.Diagnostics.Metrics;
using Agoda.IoC.Core;

namespace ShipLean.Application.Features.Backups.Job;

// shiplean.backup.runs{outcome} on the ShipLean meter (decision D9): one per run that ended, outcome "succeeded" or "failed".
// The meter comes from the host's IMeterFactory under the host's meter name, which the OpenTelemetry pipeline exports.
[RegisterSingleton]
public sealed class BackupJobMetrics
{
    // The host's meter name (TelemetryNames.Meter in the WebApi project, which Application can't see).
    public const string MeterName = "ShipLean";
    public const string RunsInstrument = "shiplean.backup.runs";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";

    private readonly Counter<long> _runs;

    public BackupJobMetrics(IMeterFactory meters)
        => _runs = meters.Create(MeterName).CreateCounter<long>(RunsInstrument, description: "Backup runs that ended, by outcome.");

    public void RunFinished(string outcome) => _runs.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}
