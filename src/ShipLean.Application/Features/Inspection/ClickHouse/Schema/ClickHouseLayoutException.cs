namespace ShipLean.Application.Features.Inspection.ClickHouse.Schema;

// The exporter's tables do not look the way the views need (a missing column). The message names the table and the columns.
public sealed class ClickHouseLayoutException : InvalidOperationException
{
    public ClickHouseLayoutException(string message)
        : base(message)
    {
    }
}
