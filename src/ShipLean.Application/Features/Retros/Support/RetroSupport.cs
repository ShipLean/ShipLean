namespace ShipLean.Application.Features.Retros.Support;

public sealed class RetroSupport : IRetroSupport
{
    // TODO: insert a retro_point_support row for the caller unless a live one exists. Until then nothing is stored.
    public Task SupportAsync(Guid pointId, CancellationToken cancellationToken) => Task.CompletedTask;

    // TODO: set withdrawn_at on the caller's live retro_point_support row, if any. Until then nothing changes.
    public Task WithdrawAsync(Guid pointId, CancellationToken cancellationToken) => Task.CompletedTask;
}
