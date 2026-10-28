using System.Security.Claims;

namespace ShipLean.WebApi.Features.LinearApi;

// The connection a request came in as: the id and name of the agent connection whose callback token it carried.
public sealed record LinearCaller(Guid ConnectionId, string Name)
{
    public static ClaimsPrincipal ToPrincipal(Guid connectionId, string name)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, connectionId.ToString()), new Claim(ClaimTypes.Name, name)],
            LinearApiPolicy.Scheme));

    public static LinearCaller Of(ClaimsPrincipal? user)
        => Guid.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? new LinearCaller(id, user.FindFirstValue(ClaimTypes.Name) ?? string.Empty)
            : throw new InvalidOperationException("The Linear-compatible endpoint ran without an authenticated connection.");
}
