using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// A task of the project: its story and Feature (null when it has none), its assignee, and AgentName, the revision.agent_name of
// its first move to an In-progress state (null when a person started it, or it never started).
public sealed record LeadTimeTaskFact(
    string Key,
    string Title,
    string? StoryKey,
    string? FeatureKey,
    string? FeatureTitle,
    Guid? AssigneeId,
    string? AssigneeName,
    string? AgentName,
    string StateName,
    StateCategory StateCategory);
