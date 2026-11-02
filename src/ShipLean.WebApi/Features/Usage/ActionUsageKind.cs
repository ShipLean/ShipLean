namespace ShipLean.WebApi.Features.Usage;

// What the usage filter does with a controller action.
public enum ActionUsageKind
{
    // The filter emits the action's event.
    Track,

    // The action emits its own event (it knows something the filter cannot see); the filter emits nothing.
    SelfEmitted,

    // No event, for a stated reason.
    Untracked,
}
