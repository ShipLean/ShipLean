namespace ShipLean.Contracts.Screens;

// A Feature's health, set by a person or an agent. On the wire as camelCase strings. Features only; null on other types.
public enum FeatureHealth
{
    OnTrack,
    AtRisk,
    Blocked,
}
