using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Users;

// Finding and creating users. Never deletes one.
public interface IUserDirectory
{
    // Exact email, case-insensitive, surrounding spaces ignored: a partial email finds nothing. Null when nobody matches.
    Task<UserDto?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    // Throws NotFound for an unknown id.
    Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    // A user with no password (status NoPassword). Throws Validation for a bad email or name, Conflict for a taken email.
    Task<UserDto> CreateAsync(string email, string name, CancellationToken cancellationToken = default);

    // Create-or-find on the email (case-insensitive) in one statement, never Conflict. A new user is named `name`, or the part
    // of the email before the @ when there is no usable name. An existing user whose stored name differs gets the new name
    // and initials. A null name, or one the name rules reject, leaves the stored name alone. Throws Validation for a bad email.
    Task<UserDto> FindOrCreateAsync(string email, string? name, CancellationToken cancellationToken = default);

    // Live members of the project whose name or email starts with the text (case-insensitive), in name order.
    Task<IReadOnlyList<UserRef>> SearchMembersAsync(Guid projectId, string text, CancellationToken cancellationToken = default);
}
