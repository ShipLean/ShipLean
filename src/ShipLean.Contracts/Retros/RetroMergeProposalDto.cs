namespace ShipLean.Contracts.Retros;

public sealed record RetroMergeProposalDto(Guid Id, RetroThemeRefDto From, RetroThemeRefDto Into, string Reason, int PointCount, RetroAuthorDto ProposedBy);
