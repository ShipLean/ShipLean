namespace ShipLean.WebApi.Features.Auth;

// Marks an action a user may call while they still have to replace their one-time password (changing it, signing out).
// Every other action answers 403 `password_change_required` until they have (PasswordChangeFilter).
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowPasswordChangeAttribute : Attribute
{
}
