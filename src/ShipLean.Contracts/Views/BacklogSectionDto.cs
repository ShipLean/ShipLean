using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views;

// A planning section: a sprint (current, then future), or the Backlog (Sprint null). Capacity is null when none is entered.
public sealed record BacklogSectionDto(string Id, string Title, SprintRef? Sprint, decimal Points, decimal? Capacity, IReadOnlyList<BacklogRowDto> Rows);
