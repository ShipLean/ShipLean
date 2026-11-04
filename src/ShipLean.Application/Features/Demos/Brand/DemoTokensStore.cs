namespace ShipLean.Application.Features.Demos.Brand;

public sealed class DemoTokensStore : IDemoTokensStore
{
    // TODO: the real store replaces this: parses the lines and keeps the normalised text.
    public Task<string> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);

    public Task SaveAsync(Guid projectId, string? tokens, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
