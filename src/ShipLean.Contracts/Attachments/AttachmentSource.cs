namespace ShipLean.Contracts.Attachments;

// Who attached a file when it was not a person in the browser: the agent's name and the MCP tool it used.
public sealed record AttachmentSource(string AgentName, string Via)
{
    // The Via of an agent session's run log, stored through its agent-log link. Only a commit whose every row carries it may replace a live run
    // log of the same name; any other upload of that name is refused (IAttachmentStore).
    public const string AgentLogUploadVia = "agent_log_upload";
}
