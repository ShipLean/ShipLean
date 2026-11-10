using ShipLean.Contracts.Inbox;

namespace ShipLean.Application.Features.Inbox;

// One open ask from an IInboxAskSource, with the work item it is on.
public sealed record InboxAskRow(Guid WorkItemId, InboxAskDto Ask);
