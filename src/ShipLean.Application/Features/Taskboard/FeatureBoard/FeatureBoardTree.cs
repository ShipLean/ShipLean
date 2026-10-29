using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Taskboard.FeatureBoard;

// Lays the Feature's descendants out as sections, depth first with the board's Feature first. The descendants read drops Removed
// items without saying where they sat, so a Feature under a Removed one is depth 1, and a story or bug whose parent isn't a
// live Feature of the tree goes in the board's own section. Order within a level, and of stories, is the read's (open first, then closed).
internal static class FeatureBoardTree
{
    public static IReadOnlyList<FeatureBoardSection> Layout(StackRowDto root, IReadOnlyList<WorkTreeItemDto> descendants)
    {
        var features = descendants.Where(d => d.Type.Id == WorkItemTypeId.Feature).ToList();
        var known = features.Select(f => f.Id).Append(root.Id).ToHashSet();
        Guid SectionOf(Guid? parentId) => parentId is { } id && known.Contains(id) ? id : root.Id;
        var children = features.ToLookup(f => SectionOf(f.ParentId));
        var stories = descendants.Where(d => d.Type.Id is WorkItemTypeId.Story or WorkItemTypeId.Bug).ToLookup(s => SectionOf(s.ParentId), s => s.Id);

        var sections = new List<FeatureBoardSection>
        {
            new(root.Id, root.Key, root.Title, root.State, 0, null, [.. stories[root.Id]]),
        };
        Walk(root.Id, 1, null);
        return sections;

        void Walk(Guid parentId, int depth, ParentRefDto? above)
        {
            foreach (var feature in children[parentId])
            {
                sections.Add(new FeatureBoardSection(feature.Id, feature.Key, feature.Title, feature.State, depth, depth >= 2 ? above : null, [.. stories[feature.Id]]));
                Walk(feature.Id, depth + 1, new ParentRefDto(feature.Id, feature.Key, feature.Type, feature.Title));
            }
        }
    }
}
