namespace ShipLean.TestSupport.Contracts.Search;

// How well a work item's arranged rows match the contract's query. A leg must put a Strong item above a Weak one, so both must land in at least one common list, above the leg's cut-off.
public enum MatchStrength
{
    Weak,
    Strong,
}
