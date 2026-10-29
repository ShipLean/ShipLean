namespace ShipLean.Contracts.Holidays;

// Told by the refresh job after a refresh changed a country's stored holidays. The default does nothing; the sprint capacity
// feature replaces it to recompute capacity. Not a ProjectEvent: a country belongs to no project.
public interface IHolidaysChanged
{
    Task ChangedAsync(string countryCode, CancellationToken cancellationToken = default);
}
