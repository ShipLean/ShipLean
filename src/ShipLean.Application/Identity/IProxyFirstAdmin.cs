namespace ShipLean.Application.Identity;

// The first instance admin in proxy mode: called by proxy sign-in before it issues the cookie. Promotes the user only
// while no instance admin exists and email equals SHIPLEAN_ADMIN_EMAIL (case-insensitive), and never rotates the
// security stamp. True when it promoted them.
public interface IProxyFirstAdmin
{
    Task<bool> PromoteIfFirstAsync(Guid userId, string email, CancellationToken cancellationToken);
}
