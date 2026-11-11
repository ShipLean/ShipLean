namespace ShipLean.Contracts.Asks;

// What happened to an ask. Raised, Edited, Confirmed, Changed and Withdrawn are decision events; Asked, Answered and
// Withdrawn are question events.
public enum AskEventKind
{
    Raised,
    Edited,
    Confirmed,
    Changed,
    Withdrawn,
    Asked,
    Answered,
}
