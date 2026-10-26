using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.UnitTest.Contracts.WorkItems;

// Builds the in-memory store the contract fixtures run against and arranges a fresh project in it.
public static class InMemoryWorkItemArrangement
{
    private static int _projects;

    public static InMemoryWorkItemStore CreateStore(IWorkItemRules rules, Guid actorId)
    {
        var store = new InMemoryWorkItemStore(rules, new FixedUser(actorId), new MarkdownRenderer(), TimeProvider.System, AgentIdentity.None);
        store.AddUser(User(SeedIds.Users.Joel, "Joel Dickson", "JD"));
        store.AddUser(User(SeedIds.Users.Priya, "Priya Shah", "PS"));
        store.AddUser(User(SeedIds.Users.Sam, "Sam Rivera", "SR"));
        return store;
    }

    // A new project on every call: its own key, its own state ids, three board columns and an import job.
    public static ArrangedProject Arrange(InMemoryWorkItemStore store)
    {
        var projectId = Guid.NewGuid();
        var key = LetterKey(Interlocked.Increment(ref _projects));
        var states = new List<StateSnapshot>();
        foreach (var type in Enum.GetValues<WorkItemTypeId>())
        {
            states.Add(new(Guid.NewGuid(), type, "New", StateCategory.Proposed));
            states.Add(new(Guid.NewGuid(), type, "Active", StateCategory.InProgress));
            states.Add(new(Guid.NewGuid(), type, "Closed", StateCategory.Completed));
            states.Add(new(Guid.NewGuid(), type, "Removed", StateCategory.Removed));
        }

        store.AddProject(projectId, key, 1000, states);
        string[] columns = [$"col-{Guid.NewGuid():N}", $"col-{Guid.NewGuid():N}", $"col-{Guid.NewGuid():N}"];
        return new ArrangedProject(projectId, key, states, columns, Guid.NewGuid());
    }

    private static UserRef User(Guid id, string name, string initials) => new(id, name, initials, "#5b6b8c", null);

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid Id { get; } = id;
    }

    // Project keys are 2-4 letters (the schema's rule): T then the number in base 26 (TA, TB, ... TZ, TBA, ...).
    private static string LetterKey(int number)
    {
        var letters = string.Empty;
        do
        {
            letters = (char)('A' + (number % 26)) + letters;
            number /= 26;
        }
        while (number > 0);
        return "T" + letters;
    }
}
