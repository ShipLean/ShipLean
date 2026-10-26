using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Fakes.WorkItems;

// Adds and removes link rows. Removing archives the row; adding what was archived revives the same row, in its old place.
// The world's per-item list holds every link the item ever had, in the order they were first added (live and archived).
internal static class InMemoryLinks
{
    public static IReadOnlyList<LinkSnapshot> Apply(InMemoryWorld world, Guid itemId, IReadOnlyList<LinkSnapshot> current, WorkItemChanges? changes)
    {
        var links = current.ToList();
        if (changes is null)
        {
            return links;
        }

        var history = world.ArchivedLinks.TryGetValue(itemId, out var existing) ? existing : [];
        foreach (var id in changes.RemoveLinkIds)
        {
            var link = links.FirstOrDefault(l => l.Id == id) ?? throw ApiException.Validation("That link isn't on this work item.");
            links.Remove(link);
        }

        foreach (var value in changes.AddLinks)
        {
            var normalised = Normalise(world, itemId, value);
            if (links.Any(l => l.Value == normalised))
            {
                continue;
            }

            var revived = history.FirstOrDefault(l => l.Value == normalised && links.All(live => live.Id != l.Id));
            if (revived is null)
            {
                revived = new LinkSnapshot(Guid.NewGuid(), normalised);
                history.Add(revived);
            }

            links.Add(revived);
        }

        world.ArchivedLinks[itemId] = history;
        return [.. links.OrderBy(l => history.ToList().FindIndex(h => h.Id == l.Id))];
    }

    private static LinkValue Normalise(InMemoryWorld world, Guid itemId, LinkValue value)
    {
        if (value.Type == LinkType.Url)
        {
            return string.IsNullOrWhiteSpace(value.Url) || value.WorkItemId is not null
                ? throw ApiException.Validation("A url link needs a url and no work item.")
                : new LinkValue(LinkType.Url, null, null, value.Url, value.Title);
        }

        if (value.WorkItemId is not { } targetId)
        {
            throw ApiException.Validation("A link between work items needs the other work item.");
        }

        if (!world.Items.TryGetValue(targetId, out var target))
        {
            throw ApiException.NotFound("Linked work item not found.");
        }

        return targetId == itemId
            ? throw ApiException.Validation("A work item can't link to itself.")
            : new LinkValue(value.Type, targetId, target.Key, null, null);
    }
}
