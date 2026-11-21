namespace ShipLean.Contracts.Prompts;

// The agent connected on this request. Off a request it is (other, None, null) and never throws.
public interface IConnectedAgent
{
    ConnectedAgent Current { get; }
}
