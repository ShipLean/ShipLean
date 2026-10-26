using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Agents;

// The user's current stamp, read through the user contracts so a disabled user or a changed password is seen at once.
internal static class AgentUserState
{
    // Null for an unknown or disabled user.
    public static async Task<string?> CurrentStampAsync(IUserDirectory users, IUserAccounts accounts, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await users.GetAsync(userId, cancellationToken);
            return user.Status == UserStatus.Disabled ? null : await accounts.GetSecurityStampAsync(userId, cancellationToken);
        }
        catch (ApiException e) when (e.Code == ApiErrorCodes.NotFound)
        {
            return null;
        }
    }
}
