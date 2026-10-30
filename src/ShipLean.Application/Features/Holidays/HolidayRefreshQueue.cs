using System.Threading.Channels;
using Agoda.IoC.Core;

namespace ShipLean.Application.Features.Holidays;

// The hand-off from a request that tracks a country to the refresh job: country codes to refresh now, in the order queued.
[RegisterSingleton]
public sealed class HolidayRefreshQueue
{
    private readonly Channel<string> _countries = Channel.CreateUnbounded<string>();

    public void Enqueue(string countryCode) => _countries.Writer.TryWrite(countryCode);

    public ValueTask<string> ReadAsync(CancellationToken cancellationToken) => _countries.Reader.ReadAsync(cancellationToken);
}
