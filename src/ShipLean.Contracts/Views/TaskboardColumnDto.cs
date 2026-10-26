using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views;

// One Task state (Removed excluded), in state order.
public sealed record TaskboardColumnDto(StateRef State, int Count);
