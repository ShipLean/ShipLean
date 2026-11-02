using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Theme merge proposals (K5).
internal sealed class RetroMergeProposalWriter(NpgsqlDataSource dataSource)
{
    // TODO(A0X): fill in per K5. Opens the connection the real body will use.
    public async Task<bool> ProposeAsync(Guid projectId, string fromTheme, string intoTheme, string reason, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        throw ApiException.Internal();
    }
}
