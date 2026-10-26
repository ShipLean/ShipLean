using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Users;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Auth;

// GET /api/me -> MeDto: who is signed in and what the shell needs to draw itself.
[Route("api/me")]
public sealed class MeController(MeReader me, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    public Task<MeDto> Get(CancellationToken ct) => me.GetAsync(currentUser.Id, ct);
}
