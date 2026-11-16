namespace ShipLean.Contracts.Agents;

// D10: an attachment that is an agent session's run log (agent_session.run_log_attachment_id) is for project admins only, like the panel's
// runLog (D5): raw agent output can hold hosts or secrets. Every attachment read a member can reach asks here: the item's attachment list,
// the attachment content route, and MCP get_work_item and read_attachment. A hidden run log answers not_found, as if it were not there.
public interface IRunLogAttachments
{
    // The ids of the item's run logs that a caller with this role may not see; empty for a project admin.
    Task<IReadOnlySet<Guid>> HiddenAsync(Guid workItemId, ProjectRole role, CancellationToken cancellationToken = default);

    // True when the attachment is a run log and a caller with this role may not see it.
    Task<bool> IsHiddenAsync(Guid attachmentId, ProjectRole role, CancellationToken cancellationToken = default);
}
