namespace ShipLean.Contracts.Users.Admin;

// The new user and the one-time password, shown once: no later read returns it. Null only in proxy mode, where nobody has a password.
public sealed record AddUserResponse(AdminUserDto User, OneTimePasswordDto? OneTimePassword);
