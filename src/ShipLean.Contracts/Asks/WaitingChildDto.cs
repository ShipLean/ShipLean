using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Asks;

// A Story or Bug under a Feature with open asks. Owner is its assignee, or its creator when unassigned (OwnerIsCreator).
// Type is the work item type id ("story", "bug").
public sealed record WaitingChildDto(
    Guid Id,
    string Key,
    string Type,
    string Title,
    UserRef? Owner,
    bool OwnerIsCreator,
    int OpenQuestions,
    int OpenDecisions);
