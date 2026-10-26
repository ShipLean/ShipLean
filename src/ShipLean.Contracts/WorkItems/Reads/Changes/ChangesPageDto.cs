namespace ShipLean.Contracts.WorkItems.Reads.Changes;

// Entries oldest first. Cursor resumes after the last entry; when there are none it is the cursor that was passed in
// (null if none was), so a caller can always keep it. More = the limit cut the page short.
public sealed record ChangesPageDto(IReadOnlyList<ChangeEntryDto> Entries, string? Cursor, bool More);
