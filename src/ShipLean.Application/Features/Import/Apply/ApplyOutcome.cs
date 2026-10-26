namespace ShipLean.Application.Features.Import.Apply;

// What applying one issue did. ItemId is null when the issue was skipped (its type maps to Skip) or failed (an import_error row was written
// and Failed is true). UnresolvedKeys: the parent or linked issues not imported yet; apply the issue again once they are.
public sealed record ApplyOutcome(Guid? ItemId, string? Key, bool Created, bool Failed, IReadOnlyList<string> UnresolvedKeys)
{
    public static ApplyOutcome Skipped { get; } = new(null, null, false, false, []);
}
