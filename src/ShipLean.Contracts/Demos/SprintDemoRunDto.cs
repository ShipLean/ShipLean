using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Demos;

// RequestedByLabel is RequestedLabel with who asked ("Requested today at 10:15 by Joel Dickson"), null when nobody is recorded.
public sealed record SprintDemoRunDto(
    string ItemKey,
    string ItemUrl,
    string? SessionState,
    string RequestedLabel,
    string? UploadExpiresLabel,
    string ItemTitle,
    WorkItemTypeId ItemType,
    string? RequestedByLabel);
