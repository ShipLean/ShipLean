namespace ShipLean.Contracts.Inspection;

// How a run came to be linked to an item (K3 link rules).
public enum LinkSource
{
    Tagged,
    FromToolCall,
    ShipleanFile,
}
