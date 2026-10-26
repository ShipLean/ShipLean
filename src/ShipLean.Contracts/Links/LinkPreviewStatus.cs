namespace ShipLean.Contracts.Links;

// How the last attempt to read the address went. Private is an address ShipLean refuses to call (not publicly routable).
public enum LinkPreviewStatus
{
    Ok,
    Unreachable,
    Private,
    None,
}
