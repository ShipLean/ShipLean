namespace ShipLean.Contracts.Users;

// Derived from app_user, first match wins: disabled, noPassword, locked, oneTimePassword, active.
public enum UserStatus
{
    Active,
    OneTimePassword,
    Locked,
    NoPassword,
    Disabled,
}
