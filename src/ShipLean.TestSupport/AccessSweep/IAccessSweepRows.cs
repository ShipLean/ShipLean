namespace ShipLean.TestSupport.AccessSweep;

// Implemented once per feature slice, in a file of its own with a parameterless constructor, in any test assembly.
// AccessSweepRows.All() finds every implementation by reflection.
public interface IAccessSweepRows
{
    IEnumerable<AccessSweepRow> Rows { get; }
}
