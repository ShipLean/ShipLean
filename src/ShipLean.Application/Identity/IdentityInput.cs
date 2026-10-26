using ShipLean.Application.Features.Users;
using ShipLean.Contracts.Errors;
using ShipLean.Core.People;

namespace ShipLean.Application.Identity;

// UserInput's email and name rules for a value an identity provider sent: null instead of a validation error. WebApi
// code uses this, never Features.Users.UserInput (feature isolation).
public static class IdentityInput
{
    // The trimmed email, or null when UserInput.RequireEmail rejects it.
    public static string? Email(string? raw)
    {
        try
        {
            return UserInput.RequireEmail(raw);
        }
        catch (ApiException)
        {
            return null;
        }
    }

    // The trimmed name, or null when it is missing, blank or refused. DisplayName is the rule UserInput.RequireName
    // throws for, so a name passes here exactly when RequireName accepts it.
    public static string? Name(string? raw) => DisplayName.Problem(raw) is null ? raw!.Trim() : null;
}
