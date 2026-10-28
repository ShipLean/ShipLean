using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.States;

// A state while a set of changes is being checked: nothing is written until every check has passed.
internal sealed class StateDraft
{
    private readonly string _originalName;
    private readonly StateCategory _originalCategory;
    private readonly int _originalOrder;
    private readonly bool _originalStartsAgent;

    public StateDraft(Guid id, WorkItemTypeId type, string name, StateCategory category, int order, bool isNew, bool startsAgent = false)
    {
        Id = id;
        Type = type;
        Name = _originalName = name;
        Category = _originalCategory = category;
        Order = _originalOrder = order;
        StartsAgent = _originalStartsAgent = startsAgent;
        IsNew = isNew;
    }

    public Guid Id { get; }

    public WorkItemTypeId Type { get; }

    public string Name { get; set; }

    public StateCategory Category { get; set; }

    public int Order { get; set; }

    public bool StartsAgent { get; set; }

    public bool IsNew { get; }

    public bool Renamed => !IsNew && Name != _originalName;

    public bool Changed => !IsNew && (Name != _originalName || Category != _originalCategory || Order != _originalOrder || StartsAgent != _originalStartsAgent);
}
