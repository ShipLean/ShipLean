using ShipLean.Contracts.Retros;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Retros.People;

// The one place a fix type becomes the priority and the tag of a new action item: the more deterministic and earlier the feedback, the higher the priority.
// Product and other have no tier: they get neither a priority nor a tag, so the item keeps what a new action item gets without one.
internal static class RetroFixTiers
{
    public static int? Priority(RetroFixType fixType) => fixType switch
    {
        RetroFixType.Fix => 1,
        RetroFixType.Structure => 2,
        RetroFixType.Analyzer => 3,
        RetroFixType.Test or RetroFixType.Skill or RetroFixType.Guide => 4,
        _ => null,
    };

    public static string? Tag(RetroFixType fixType) => Priority(fixType) is null ? null : $"fix:{fixType.ToString().ToLowerInvariant()}";

    // The changes a new action item of this fix type is created with; null when the type has no tier.
    public static WorkItemChanges? Changes(RetroFixType? fixType)
        => fixType is { } type && Priority(type) is { } priority && Tag(type) is { } tag
            ? new WorkItemChanges { Priority = new FieldSet<int>(priority), Tags = new FieldSet<IReadOnlyList<string>>([tag]) }
            : null;
}
