namespace ShipLean.Contracts.Screens;

// What a digest line reports. On the wire as camelCase strings.
public enum DigestLineKind
{
    State,
    Health,
    StatusLine,
    Comment,
    AgentWork,
    Ask,
}
