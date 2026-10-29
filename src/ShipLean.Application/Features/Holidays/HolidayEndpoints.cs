using Agoda.IoC.Core;
using ShipLean.Contracts.Holidays;

namespace ShipLean.Application.Features.Holidays;

// The country list for the Public holidays dropdown. Not project data: any signed-in user may read it.
[RegisterPerRequest]
public sealed class HolidayEndpoints(IPublicHolidays holidays)
{
    public Task<IReadOnlyList<HolidayCountryDto>> CountriesAsync(CancellationToken cancellationToken) => holidays.CountriesAsync(cancellationToken);
}
