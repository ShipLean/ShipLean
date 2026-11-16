using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Links;

// The one rule for a Caused by link (SHIP-1398 item D18): it runs from a Bug to the Task that introduced it, and joins nothing else.
// Every writer of links (the link endpoint, an item patch, the link tool) asks it, so none can drift.
public static class CausedByRule
{
    public static void Require(LinkType type, WorkItemTypeId source, WorkItemTypeId target)
    {
        if (type == LinkType.CausedBy && (source != WorkItemTypeId.Bug || target != WorkItemTypeId.Task))
        {
            throw ApiException.Validation($"A Caused by link goes from a Bug to the Task that introduced it, not from a {WorkItemTypeRules.Name(source)} to a {WorkItemTypeRules.Name(target)}.", "type");
        }
    }
}
