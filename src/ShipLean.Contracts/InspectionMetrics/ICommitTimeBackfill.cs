namespace ShipLean.Contracts.InspectionMetrics;

// The temporary backfill of commit times on commit links (SHIP-1398 K1, item D13). Only instance admins may use it: for anyone
// else both methods throw NotFound, so the tool's existence is not revealed.
// TODO: temporary; remove once existing installs are backfilled
public interface ICommitTimeBackfill
{
    // The live commit links with no time yet, oldest link first, at most limit. Pass the previous page's NextCursor as after.
    Task<CommitTimeMissingPageDto> ListMissingAsync(int limit, string? after, CancellationToken cancellationToken = default);

    // Sets the time on each live commit link whose 7-character sha matches, only while it has none (ILinkCards.SetCommitTimeAsync's guard).
    Task<CommitTimeBackfillCountsDto> SetAsync(IReadOnlyList<CommitTimeDto> commits, CancellationToken cancellationToken = default);
}

public sealed record CommitTimeDto(string Sha, DateTimeOffset CommittedAt);

public sealed record MissingCommitTimeDto(string Sha7, string Url, string ItemKey);

public sealed record CommitTimeMissingPageDto(IReadOnlyList<MissingCommitTimeDto> Links, string? NextCursor);

// Updated, AlreadySet and NotFound count links, except NotFound which counts the given shas that match no commit link.
public sealed record CommitTimeBackfillCountsDto(int Updated, int AlreadySet, int NotFound);
