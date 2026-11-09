namespace ShipLean.Contracts.Asks;

// Everything waiting on the signed-in person: open asks on the items they own, across their projects. The counts are
// totals over Items. Items are ordered by their oldest open ask, oldest first.
public sealed record WaitingDto(int OpenQuestions, int OpenDecisions, IReadOnlyList<WaitingItemDto> Items);
