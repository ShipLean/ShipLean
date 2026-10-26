using ShipLean.WebApi.Features.Mcp.Plan;

namespace ShipLean.WebApi.Tests.Mcp.Plan;

// PlanBody holds a list, so record equality compares the list by reference: compare the target and each item.
internal sealed class PlanBodyComparer : IEqualityComparer<PlanBody>
{
    public bool Equals(PlanBody? x, PlanBody? y) => x != null && y != null && x.Target == y.Target && x.Items.SequenceEqual(y.Items);

    public int GetHashCode(PlanBody obj) => obj.Target.GetHashCode(StringComparison.Ordinal);
}
