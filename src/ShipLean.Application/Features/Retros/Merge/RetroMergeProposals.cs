using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Merge;

// The proposal is looked up for real (an unknown or archived one is not_found); answering it changes nothing.
// TODO: the real merge and keep-apart replace this.
public sealed class RetroMergeProposals(NpgsqlDataSource dataSource) : IRetroMergeProposals
{
    public Task MergeAsync(Guid proposalId, RetroActor actor, CancellationToken cancellationToken = default) => RequireAsync(proposalId, cancellationToken);

    public Task KeepApartAsync(Guid proposalId, RetroActor actor, CancellationToken cancellationToken = default) => RequireAsync(proposalId, cancellationToken);

    private async Task RequireAsync(Guid proposalId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var found = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM retro_theme_merge_proposal WHERE id = @proposalId AND archived_at IS NULL)",
            new { proposalId },
            cancellationToken: cancellationToken));
        if (!found)
        {
            throw ApiException.NotFound("No merge proposal with that id was found.");
        }
    }
}
