using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Users;

// The safety rules on disabling and demoting, shared by the Postgres and in-memory accounts so they cannot drift:
// never yourself, and one active instance admin always remains.
public static class AccountRules
{
    public static void CheckDisable(Guid target, Guid caller, bool targetIsActiveAdmin, bool anotherActiveAdminExists)
    {
        if (target == caller)
        {
            throw ApiException.Validation("You cannot disable your own account.");
        }
        CheckLastAdmin(targetIsActiveAdmin, anotherActiveAdminExists);
    }

    public static void CheckDemote(Guid target, Guid caller, bool targetIsActiveAdmin, bool anotherActiveAdminExists)
    {
        if (target == caller)
        {
            throw ApiException.Validation("You cannot remove your own admin rights.");
        }
        CheckLastAdmin(targetIsActiveAdmin, anotherActiveAdminExists);
    }

    private static void CheckLastAdmin(bool targetIsActiveAdmin, bool anotherActiveAdminExists)
    {
        if (targetIsActiveAdmin && !anotherActiveAdminExists)
        {
            throw ApiException.Validation("ShipLean needs at least one active instance admin.");
        }
    }

    public static ApiException UserNotFound(Guid id) => ApiException.NotFound($"User {id} was not found.");

    public static string NewStamp() => Guid.NewGuid().ToString("N");
}
