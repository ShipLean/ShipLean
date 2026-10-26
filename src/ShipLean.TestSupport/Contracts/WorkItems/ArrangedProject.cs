using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.TestSupport.Contracts.WorkItems;

// A new project (new key, new state ids) for one test: its states in state order, the ids of the board columns the tests
// move cards into (in board order), and an import job the imports are filed under.
public sealed record ArrangedProject(Guid ProjectId, string Key, IReadOnlyList<StateSnapshot> States, IReadOnlyList<string> ColumnIds, Guid ImportJobId)
{
    public Guid StateId(WorkItemTypeId type, StateCategory category)
        => States.First(s => s.Type == type && s.Category == category).Id;

    public string ColumnId(int index) => ColumnIds[index];
}
