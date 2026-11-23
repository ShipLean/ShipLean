using System.Diagnostics.Metrics;
using ShipLean.Application.Features.WebVitals;
using ShipLean.Contracts.Analytics;

namespace ShipLean.UnitTest.Application.WebVitals;

[TestFixture]
public class WebVitalsQueueTests
{
    private static WebVitalsSample Sample(int n)
        => new("taskboard", WebVitalsNavigation.Hard, n, null, null, null, null, null, null, "user:x", DateTimeOffset.UnixEpoch);

    [Test]
    public void Track_WhenFull_ShouldDropAndCountWithoutThrowing()
    {
        using var factory = new OwnMeterFactory();
        long dropped = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Scope == factory && instrument.Name == WebVitalsQueue.DroppedCounterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref dropped, value));
        listener.Start();
        var queue = new WebVitalsQueue(factory);
        for (var i = 0; i < WebVitalsQueue.Capacity; i++)
        {
            queue.Track(Sample(i));
        }
        Interlocked.Read(ref dropped).ShouldBe(0);

        Should.NotThrow(() => queue.Track(Sample(-1)));

        queue.Dropped.ShouldBe(1);
        Interlocked.Read(ref dropped).ShouldBe(1);
        queue.Reader.Count.ShouldBe(WebVitalsQueue.Capacity);
        queue.Reader.TryRead(out var first).ShouldBeTrue();
        first.Lcp.ShouldBe(0);
    }

    // A meter factory of the test's own, so the listener sees only this queue's counter.
    private sealed class OwnMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options.Name, options.Version, options.Tags, this);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
