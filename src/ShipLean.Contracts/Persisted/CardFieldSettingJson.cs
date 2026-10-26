using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Persisted;

public sealed record CardFieldSettingJson(WorkItemTypeId Type, bool ShowKey, bool ShowAssignee, bool ShowTags, bool ShowPoints, bool ShowEmptyFields, IReadOnlyList<string> ExtraFields);
