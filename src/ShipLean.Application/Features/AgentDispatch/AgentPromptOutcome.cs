namespace ShipLean.Application.Features.AgentDispatch;

// What AgentPrompter did with a follow-up.
public enum AgentPromptOutcome
{
    // A `prompted` delivery is queued.
    Queued,

    // The item has no live session, so nothing is queued.
    NoLiveSession,

    // Stop was already requested on the live session; nothing more is queued.
    AlreadyStopping,
}
