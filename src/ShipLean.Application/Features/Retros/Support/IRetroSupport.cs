namespace ShipLean.Application.Features.Retros.Support;

// The current user's +1 on a retro point, and taking it back. Both are idempotent: supporting twice keeps one support, and
// withdrawing what isn't there changes nothing. Callers check Member access to the point's project first.
public interface IRetroSupport
{
    Task SupportAsync(Guid pointId, CancellationToken cancellationToken);

    Task WithdrawAsync(Guid pointId, CancellationToken cancellationToken);
}
