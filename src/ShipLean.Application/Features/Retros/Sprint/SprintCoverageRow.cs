namespace ShipLean.Application.Features.Retros.Sprint;

// A work item that counts toward coverage: a story-level item finished in the sprint, or a feature with a child in it.
internal sealed record SprintCoverageRow(Guid Id, string Key, int Number, string Type, string Title, bool IsFeature, bool LastChildCompleted, bool HasRetro);
