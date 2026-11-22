using System.Diagnostics.Metrics;
using System.Threading.Channels;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.WebVitals;

// The real IWebVitalsQueue: a bounded in-memory queue, separate from the usage queue. Track never blocks and never throws; a full
// queue drops the sample and counts it on shiplean.web_vitals.dropped. The dispatcher drains Reader.
public sealed class WebVitalsQueue : IWebVitalsQueue
{
    public const int Capacity = 2_000;
    public const string MeterName = "ShipLean";
    public const string DroppedCounterName = "shiplean.web_vitals.dropped";

    private readonly Channel<WebVitalsSample> _channel = Channel.CreateBounded<WebVitalsSample>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    private readonly Counter<long> _dropped;
    private long _droppedCount;

    public WebVitalsQueue(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _dropped = meter.CreateCounter<long>(DroppedCounterName);
    }

    public ChannelReader<WebVitalsSample> Reader => _channel.Reader;

    public long Dropped => Interlocked.Read(ref _droppedCount);

    public void Track(WebVitalsSample sample)
    {
        if (_channel.Writer.TryWrite(sample))
        {
            return;
        }
        Interlocked.Increment(ref _droppedCount);
        _dropped.Add(1);
    }
}
