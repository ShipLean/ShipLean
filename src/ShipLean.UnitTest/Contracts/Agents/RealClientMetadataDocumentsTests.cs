using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Agents.Clients;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.UnitTest.Agents.Clients;

namespace ShipLean.UnitTest.Contracts.Agents;

// The real fetcher against the shared contract, with the network stubbed to serve one valid document. Lives here, not with
// the fetcher's own tests, because only this folder may inherit a contract base class.
public sealed class RealClientMetadataDocumentsTests : ClientMetadataDocumentsContract
{
    private readonly StubHandler _handler = new((_, _) => Task.FromResult(MetadataDocuments.Ok(MetadataDocuments.Json())));
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    protected override IClientMetadataDocuments Documents
        => new ClientMetadataDocuments(new HandlerHttpClientFactory(_handler), _clock, ClientMetadataOptions.Default);

    protected override string KnownDocumentUrl => MetadataDocuments.Url;

    [TearDown]
    public void DisposeHandler() => _handler.Dispose();
}
