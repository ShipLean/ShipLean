using ShipLean.Contracts.Holidays;

namespace ShipLean.Application.Features.Holidays;

// The default IHolidaysChanged: nobody listens yet.
public sealed class NoHolidaysChanged : IHolidaysChanged
{
    public Task ChangedAsync(string countryCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
