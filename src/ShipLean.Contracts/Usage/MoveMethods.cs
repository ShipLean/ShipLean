using ShipLean.Contracts.Errors;

namespace ShipLean.Contracts.Usage;

// The values of the method usage property on work_item.moved: how the user moved the card, as the client sends it in the
// move request. A request without one records no method, never a default.
public static class MoveMethods
{
    public const string Drag = "drag";
    public const string TouchDrag = "touch-drag";
    public const string Keyboard = "keyboard";
    public const string Sheet = "sheet";
    public const string PickUp = "pick-up";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Drag, TouchDrag, Keyboard, Sheet, PickUp };

    // Call after the access check and before any write: an unknown method is a validation error, a known one goes on the
    // request's usage event, and a missing one adds nothing.
    public static void Record(IUsageScope scope, string? method)
    {
        if (method is null)
        {
            return;
        }

        if (!All.Contains(method))
        {
            throw ApiException.Validation("That move method isn't one of drag, touch-drag, keyboard, sheet or pick-up.", "method");
        }

        scope.AddProperty(UsageProps.Method, method);
    }
}
