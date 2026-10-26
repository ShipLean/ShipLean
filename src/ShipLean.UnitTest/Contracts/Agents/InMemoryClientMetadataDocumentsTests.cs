using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Fakes.Agents;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Contracts.Agents;

namespace ShipLean.UnitTest.Contracts.Agents;

public sealed class InMemoryClientMetadataDocumentsTests : ClientMetadataDocumentsContract
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    protected override IClientMetadataDocuments Documents => new InMemoryClientMetadataDocuments(_clock);

    protected override string KnownDocumentUrl => InMemoryAgentStore.ClaudeCode.ClientId;

    [Test]
    public async Task Fetch_WhenUnknownHttpsDocument_ShouldReturnNull()
    {
        (await Documents.FetchAsync("https://other.example.test/client.json")).ShouldBeNull();
    }

    [Test]
    public async Task Fetch_WhenKnownDocument_ShouldExpireInOneHour()
    {
        var document = await Documents.FetchAsync(KnownDocumentUrl);

        document!.ExpiresAt.ShouldBe(_clock.GetUtcNow().AddHours(1));
    }
}
