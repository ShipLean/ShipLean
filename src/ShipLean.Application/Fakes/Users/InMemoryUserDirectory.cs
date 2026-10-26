using ShipLean.Application.Features.Users;
using ShipLean.Core.People;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Users;

public sealed class InMemoryUserDirectory(InMemoryUsers users) : IUserDirectory
{
    public Task<UserDto?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var wanted = (email ?? string.Empty).Trim();
        lock (users.Gate)
        {
            var row = users.Rows.FirstOrDefault(r => string.Equals(r.Email, wanted, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(row is null ? null : (UserDto?)row.ToDto(users.Clock));
        }
    }

    public Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (users.Gate)
        {
            var row = users.Rows.FirstOrDefault(r => r.Id == id) ?? throw AccountRules.UserNotFound(id);
            return Task.FromResult(row.ToDto(users.Clock));
        }
    }

    public Task<UserDto> CreateAsync(string email, string name, CancellationToken cancellationToken = default)
    {
        var cleanEmail = UserInput.RequireEmail(email);
        var cleanName = UserInput.RequireName(name);
        lock (users.Gate)
        {
            if (users.Rows.Any(r => string.Equals(r.Email, cleanEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw ApiException.Conflict($"A user with the email {cleanEmail} already exists.");
            }
            return Task.FromResult(users.Add(cleanEmail, cleanName, passwordHash: null));
        }
    }

    public Task<UserDto> FindOrCreateAsync(string email, string? name, CancellationToken cancellationToken = default)
    {
        var cleanEmail = UserInput.RequireEmail(email);
        var usable = UserInput.UsableName(name);
        lock (users.Gate)
        {
            var row = users.Rows.FirstOrDefault(r => string.Equals(r.Email, cleanEmail, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                return Task.FromResult(users.Add(cleanEmail, usable ?? UserInput.NameFromEmail(cleanEmail), passwordHash: null));
            }
            if (usable is not null && row.Name != usable)
            {
                row.Name = usable;
                row.Initials = Initials.From(usable);
                row.UpdatedAt = users.Clock.GetUtcNow();
            }
            return Task.FromResult(row.ToDto(users.Clock));
        }
    }

    public Task<IReadOnlyList<UserRef>> SearchMembersAsync(Guid projectId, string text, CancellationToken cancellationToken = default)
    {
        var prefix = (text ?? string.Empty).Trim();
        lock (users.Gate)
        {
            IReadOnlyList<UserRef> found = users.Memberships
                .Where(m => m.ProjectId == projectId && !m.Archived)
                .Select(m => users.Rows.First(r => r.Id == m.UserId))
                .Where(r => r.DisabledAt is null)
                .Where(r => r.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || r.Email.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Name.ToLowerInvariant(), StringComparer.Ordinal)
                .ThenBy(r => r.Id)
                .Select(r => new UserRef(r.Id, r.Name, r.Initials, r.AvatarColour, null))
                .ToList();
            return Task.FromResult(found);
        }
    }
}
