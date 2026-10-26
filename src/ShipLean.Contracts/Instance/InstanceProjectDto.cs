using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Instance;

public sealed record InstanceProjectDto(
    Guid Id, string Key, string Name, int MemberCount, IReadOnlyList<UserRef> Admins, bool HasActiveAdmin, int ItemCount, ProjectRole? YourRole);
