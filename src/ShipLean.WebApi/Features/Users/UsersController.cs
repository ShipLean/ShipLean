using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Users;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Users;

[Route("api/users")]
public sealed class UsersController(IUserDirectory directory) : ApiControllerBase
{
    // Any user may call it (it is how members are added); it reveals only what a UserRef holds.
    [HttpGet("by-email")]
    [ProducesResponseType<UserRef>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<UserRef> ByEmail([FromQuery] string email, CancellationToken cancellationToken)
        => UserRefs.From(await directory.FindByEmailAsync(email, cancellationToken)
            ?? throw ApiException.NotFound("No user has that email address."));
}
