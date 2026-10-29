using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Holidays;
using ShipLean.Contracts.Holidays;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Holidays;

// Public holidays by country. Any signed-in user may read the country list.
[Route("api/holidays")]
public sealed class HolidaysController(HolidayEndpoints endpoints) : ApiControllerBase
{
    [HttpGet("countries")]
    [ProducesResponseType<IReadOnlyList<HolidayCountryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<HolidayCountryDto>> Countries(CancellationToken cancellationToken) => endpoints.CountriesAsync(cancellationToken);
}
