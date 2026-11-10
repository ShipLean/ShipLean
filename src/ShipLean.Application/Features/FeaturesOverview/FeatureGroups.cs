using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;

namespace ShipLean.Application.Features.FeaturesOverview;

// Lays the Features out in the overview's groups: Active (in progress), Resolved, New (proposed), then Closed (completed). A group
// with no Features is left out. Closed is folded: it carries its count and no rows. Inside a group the rows keep backlog rank order.
public static class FeatureGroups
{
    private static readonly (StateCategory Category, string Text, string Label, bool Folded)[] Order =
    [
        (StateCategory.InProgress, "inProgress", "Active", false),
        (StateCategory.Resolved, "resolved", "Resolved", false),
        (StateCategory.Proposed, "proposed", "New", false),
        (StateCategory.Completed, "completed", "Closed", true),
    ];

    public static IReadOnlyList<FeatureGroupDto> Group(IEnumerable<FeatureRowDto> rows)
    {
        var byCategory = rows.ToLookup(r => r.State.Category);
        return
        [
            .. Order
                .Where(g => byCategory[g.Category].Any())
                .Select(g =>
                {
                    var inGroup = byCategory[g.Category].OrderBy(r => r.Rank).ToList();
                    return new FeatureGroupDto(g.Text, g.Label, g.Folded, inGroup.Count, g.Folded ? [] : inGroup);
                }),
        ];
    }
}
