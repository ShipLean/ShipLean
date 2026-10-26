namespace ShipLean.Contracts.Views;

// Epic and NoEpic lanes are generated on the Features board; the rest come from the stored swimlanes.
public enum BoardLaneKind
{
    Criteria,
    Manual,
    Default,
    Epic,
    NoEpic,
}
