namespace ShipLean.Contracts.Backlog;

// The most rows one bulk action touches, everywhere: the bulk and add endpoints, the duplicate check and the MCP planning tools.
public static class BulkLimits
{
    public const int MaxRows = 100;
}
