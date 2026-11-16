namespace ShipLean.Contracts.Inspection;

public sealed record AssembledRun(RunSummary Summary, bool LogOnly, IReadOnlyList<Turn> Turns, IReadOnlyList<RunMarker> Markers, string? WhatItDid);
