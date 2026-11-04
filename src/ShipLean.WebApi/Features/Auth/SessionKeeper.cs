using Agoda.IoC.Core;
using ShipLean.Contracts.Auth;

namespace ShipLean.WebApi.Features.Auth;

// What other features reach the sign-in cookie through: a new cookie for the user under their current stamp.
[RegisterSingleton]
public sealed class SessionKeeper(CookieSession cookie) : ISessionKeeper
{
    public Task KeepAsync(Guid userId, CancellationToken cancellationToken) => cookie.SignInAsync(userId, cancellationToken);
}
