using ShipLean.Application.Features.Users;
using ShipLean.Application.Identity;
using ShipLean.Core.People;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Users;

// The users and project memberships the two in-memory fakes share: a lane test creates a user through the directory,
// then issues a one-time password through the accounts. One lock guards everything; no I/O, no events. Nothing is removed:
// a membership is archived.
public sealed class InMemoryUsers(TimeProvider? clock = null)
{
    internal sealed class Row
    {
        public required Guid Id { get; init; }
        public required string Email { get; set; }
        public required string Name { get; set; }
        public required string Initials { get; set; }
        public required string AvatarColour { get; init; }
        public string? PasswordHash { get; set; }
        public bool MustChangePassword { get; set; }
        public required string SecurityStamp { get; set; }
        public int FailedSignInCount { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
        public bool IsAdmin { get; set; }
        public DateTimeOffset? DisabledAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? LastSignInAt { get; set; }
    }

    internal sealed record Membership(Guid ProjectId, Guid UserId)
    {
        public bool Archived { get; set; }
    }

    internal object Gate { get; } = new();

    internal List<Row> Rows { get; } = [];

    internal List<Membership> Memberships { get; } = [];

    internal TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    // Arranges a user directly, in any state (the directory's own CreateAsync only makes NoPassword users).
    public UserDto Add(
        string email, string name, string? passwordHash, bool mustChangePassword = false, DateTimeOffset? lockedUntil = null,
        DateTimeOffset? disabledAt = null, bool isInstanceAdmin = false)
    {
        lock (Gate)
        {
            var row = new Row
            {
                Id = Guid.CreateVersion7(),
                Email = email,
                Name = name,
                Initials = Initials.From(name),
                AvatarColour = AvatarPalette.For(email),
                PasswordHash = passwordHash,
                MustChangePassword = mustChangePassword,
                SecurityStamp = AccountRules.NewStamp(),
                LockedUntil = lockedUntil,
                DisabledAt = disabledAt,
                IsAdmin = isInstanceAdmin,
                UpdatedAt = Clock.GetUtcNow(),
            };
            Rows.Add(row);
            return row.ToDto(Clock);
        }
    }

    // For tests: how many users have the email (case-insensitive).
    public int CountWithEmail(string email)
    {
        lock (Gate)
        {
            return Rows.Count(r => string.Equals(r.Email, email, StringComparison.OrdinalIgnoreCase));
        }
    }

    // For tests: when the user last signed in; null when never.
    public DateTimeOffset? LastSignInAt(Guid userId)
    {
        lock (Gate)
        {
            return Rows.Single(r => r.Id == userId).LastSignInAt;
        }
    }

    // Adds the user to the project, reviving an archived membership.
    public void AddMembership(Guid projectId, Guid userId)
    {
        lock (Gate)
        {
            var existing = Memberships.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId);
            if (existing is null)
            {
                Memberships.Add(new Membership(projectId, userId));
            }
            else
            {
                existing.Archived = false;
            }
        }
    }

    public void ArchiveMembership(Guid projectId, Guid userId)
    {
        lock (Gate)
        {
            Memberships.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId)?.Archived = true;
        }
    }
}
