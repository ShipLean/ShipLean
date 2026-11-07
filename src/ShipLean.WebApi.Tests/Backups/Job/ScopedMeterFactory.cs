using System.Diagnostics.Metrics;

namespace ShipLean.WebApi.Tests.Backups.Job;

// An IMeterFactory of the test's own: every meter it makes has it as Scope, so a listener can tell this test's
// measurements from those of any other host or test running at the same time.
internal sealed class ScopedMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Tags = options.Tags, Scope = this });
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
        _meters.Clear();
    }
}
