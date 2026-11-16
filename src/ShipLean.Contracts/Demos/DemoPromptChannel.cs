namespace ShipLean.Contracts.Demos;

// Who reads the rendered demo prompt (C4). Session: an agent session ShipLean started for the demo (Create demo, Automatic demos), run by a
// relay with the demo tool profile, whose final response is posted as a comment on the demo item. Handover: the agent that called the
// create_sprint_demo MCP tool, with its own tools and the ShipLean MCP.
public enum DemoPromptChannel
{
    Session,
    Handover,
}
