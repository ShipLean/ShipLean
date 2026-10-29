using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Sprints;

// One entered capacity row with the member's UserRef columns.
public sealed record CapacityRow(Guid Id, string Name, string Initials, string AvatarColour, string? PhotoUrl, decimal? Days, decimal Points, string? Note)
{
    public MemberCapacityDto ToDto() => new(new UserRef(Id, Name, Initials, AvatarColour, PhotoUrl), Days, Points, Note);
}
