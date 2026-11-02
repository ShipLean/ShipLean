using System.Diagnostics.Metrics;
using System.Threading.Channels;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The real IUsageEvents: a bounded in-memory queue. Track never blocks and never throws; a full queue drops the event and
// counts it on shiplean.usage.dropped. The dispatcher drains Reader.
public sealed class UsageQueue : IUsageEvents
{
    public const int Capacity = 10_000;
    public const string MeterName = "ShipLean";
    public const string DroppedCounterName = "shiplean.usage.dropped";

    private readonly Channel<UsageEvent> _channel = Channel.CreateBounded<UsageEvent>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    private readonly Counter<long> _dropped;
    private long _droppedCount;

    public UsageQueue(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _dropped = meter.CreateCounter<long>(DroppedCounterName);
    }

    public ChannelReader<UsageEvent> Reader => _channel.Reader;

    public long Dropped => Interlocked.Read(ref _droppedCount);

    public void Track(UsageEvent usageEvent)
    {
        if (_channel.Writer.TryWrite(usageEvent))
        {
            return;
        }
        Interlocked.Increment(ref _droppedCount);
        _dropped.Add(1);
    }
}
