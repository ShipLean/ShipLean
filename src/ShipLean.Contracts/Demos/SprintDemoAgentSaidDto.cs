namespace ShipLean.Contracts.Demos;

// A Failed demo whose agent session ended with words, split for the "It said" block: Message is ShipLean's sentence without
// the quote, Text the agent's words (the same excerpt StatusMessage quotes). Null in every other state.
public sealed record SprintDemoAgentSaidDto(string Message, string Text);
