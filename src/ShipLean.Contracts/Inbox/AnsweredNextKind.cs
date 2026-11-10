namespace ShipLean.Contracts.Inbox;

// The first thing that happened on an item after an answer: an agent picked it up (an agent comment or revision), or
// another person replied.
public enum AnsweredNextKind
{
    AgentPickedUp,
    PersonReplied,
}
