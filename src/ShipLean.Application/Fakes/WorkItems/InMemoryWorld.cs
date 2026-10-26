using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Fakes.WorkItems;

// Everything the fake store holds. A write works on a clone and swaps it in only when it succeeds, which is the fake's
// transaction.
internal sealed class InMemoryWorld
{
    public IDictionary<Guid, InMemoryProject> Projects { get; private init; } = new Dictionary<Guid, InMemoryProject>();

    public IDictionary<Guid, WorkItemSnapshot> Items { get; private init; } = new Dictionary<Guid, WorkItemSnapshot>();

    public IDictionary<Guid, IList<RevisionSnapshot>> Revisions { get; private init; } = new Dictionary<Guid, IList<RevisionSnapshot>>();

    public IDictionary<Guid, IList<LinkSnapshot>> ArchivedLinks { get; private init; } = new Dictionary<Guid, IList<LinkSnapshot>>();

    public InMemoryProject? FindProject(Guid id) => Projects.TryGetValue(id, out var project) ? project : null;

    public WorkItemSnapshot? FindItem(Guid id) => Items.TryGetValue(id, out var item) ? item : null;

    public InMemoryWorld Clone() => new()
    {
        Projects = Projects.ToDictionary(p => p.Key, p => p.Value.Clone()),
        Items = Items.ToDictionary(i => i.Key, i => i.Value),
        Revisions = Revisions.ToDictionary(r => r.Key, r => (IList<RevisionSnapshot>)new List<RevisionSnapshot>(r.Value)),
        ArchivedLinks = ArchivedLinks.ToDictionary(r => r.Key, r => (IList<LinkSnapshot>)new List<LinkSnapshot>(r.Value)),
    };
}
