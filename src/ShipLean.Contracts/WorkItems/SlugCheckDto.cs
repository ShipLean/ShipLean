namespace ShipLean.Contracts.WorkItems;

// The answer to "can a Feature in this project use this slug?". Reason says why not for an invalid slug and is left out
// (null) when the slug is available or merely taken.
public sealed record SlugCheckDto(string Value, bool Available, string? Reason);
