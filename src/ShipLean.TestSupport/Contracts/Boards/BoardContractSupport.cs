using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.TestSupport.Contracts.Boards;

// Helpers the board settings contract shares: a project's states as the seed's SHIP has them, and a document comparison.
public static class BoardContractSupport
{
    public static IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> StandardStates() => new Dictionary<WorkItemTypeId, IReadOnlyList<StateRef>>
    {
        [WorkItemTypeId.Epic] = Workflow("New", "Active", "Resolved", "Closed"),
        [WorkItemTypeId.Feature] = Workflow("New", "Active", "Resolved", "Closed"),
        [WorkItemTypeId.Story] = Workflow("New", "Active", "Resolved", "Closed"),
        [WorkItemTypeId.Bug] = Workflow("New", "Active", "Resolved", "Closed"),
        [WorkItemTypeId.Task] = Workflow("New", "Active", "In review", "Closed"),
    };

    // The states of the seeded SHIP project, from the pinned refs.json: Bug has New / Triaged / Active / Resolved / Closed.
    public static IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> SeedStates()
    {
        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", "refs.json")))!;
        return file["stateRefs"]!.AsObject().ToDictionary(
            p => Enum.Parse<WorkItemTypeId>(p.Key, ignoreCase: true),
            p => (IReadOnlyList<StateRef>)p.Value.Deserialize<List<StateRef>>(PersistedJson.Options)!);
    }

    // The four live categories in order, then Removed.
    private static List<StateRef> Workflow(string proposed, string inProgress, string resolved, string completed) =>
    [
        new(Guid.NewGuid(), proposed, StateCategory.Proposed),
        new(Guid.NewGuid(), inProgress, StateCategory.InProgress),
        new(Guid.NewGuid(), resolved, StateCategory.Resolved),
        new(Guid.NewGuid(), completed, StateCategory.Completed),
        new(Guid.NewGuid(), "Removed", StateCategory.Removed),
    ];

    // The document as text, so two documents compare by content.
    public static string Text(BoardSettingsJson settings) => JsonSerializer.Serialize(settings, PersistedJson.Options);
}
