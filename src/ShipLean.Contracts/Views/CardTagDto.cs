using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Views;

// Colour is null for a tag the board gives no colour.
public sealed record CardTagDto(string Name, Swatch? Colour);
