using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Not needed and agent theme assignment (K5). The access check and the shape checks run before these.
internal sealed class RetroDecisionWriter(NpgsqlDataSource dataSource)
{
    // TODO(A0X): fill in per K5. Opens the connection the real body will use.
    public async Task<RetroPointDto> MarkNotNeededAsync(Guid pointId, RetroNotNeededPut request, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        throw ApiException.Internal();
    }

    // TODO(A0X): fill in per K5.
    public async Task<RetroPointDto> UndoNotNeededAsync(Guid pointId, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        throw ApiException.Internal();
    }

    // TODO(A0X): fill in per K5.
    public async Task<bool> AssignThemeAsync(Guid pointId, string themeName, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        throw ApiException.Internal();
    }
}
