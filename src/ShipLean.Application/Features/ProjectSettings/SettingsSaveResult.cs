using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.ProjectSettings;

// The saved document, and the users it removed from the project (the caller drops their live connections).
public sealed record SettingsSaveResult(ProjectSettingsDto Settings, IReadOnlyList<Guid> RemovedUserIds);
