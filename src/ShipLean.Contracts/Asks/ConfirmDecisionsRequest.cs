namespace ShipLean.Contracts.Asks;

// Confirm these decisions by number. Numbers no longer open are skipped.
public sealed record ConfirmDecisionsRequest(IReadOnlyList<int> Numbers);
