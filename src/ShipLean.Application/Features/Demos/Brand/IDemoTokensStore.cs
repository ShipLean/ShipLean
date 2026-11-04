namespace ShipLean.Application.Features.Demos.Brand;

public interface IDemoTokensStore
{
    Task<string> ReadAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveAsync(Guid projectId, string? tokens, CancellationToken cancellationToken = default);
}
