using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Fakes.WorkItems;

// One project's key sequence and states.
internal sealed class InMemoryProject(string key, int nextNumber, IReadOnlyList<StateSnapshot> states)
{
    public string Key { get; } = key;

    public int NextNumber { get; set; } = nextNumber;

    public IReadOnlyList<StateSnapshot> States { get; } = states;

    public InMemoryProject Clone() => new(Key, NextNumber, States);
}
