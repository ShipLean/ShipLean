using Agoda.IoC.Core;

namespace ShipLean.WebApi.Features.Mcp.Usage;

// Where get_work_item leaves the type of the item it read (a lower-case word, such as feature or story) for the usage filter,
// which reads it in the same request scope and sends it as item_type. Null for every other tool.
[RegisterPerRequest]
public sealed class McpItemTypeSlot
{
    public string? Value { get; set; }
}
