using Shouldly;
using ShipLean.Contracts.Agents;

namespace ShipLean.TestSupport.Contracts.Agents;

// What every IClientMetadataDocuments does with a client_id it should not fetch, and with a document it can serve. The
// refusals that need a network (private address, redirect, size, bad content) are the real fetcher's own tests.
[TestFixture]
public abstract class ClientMetadataDocumentsContract
{
    protected abstract IClientMetadataDocuments Documents { get; }

    // The URL of a document this implementation serves: an https URL with a path, whose document is valid.
    protected abstract string KnownDocumentUrl { get; }

    [TestCase("http://claude.ai/oauth/claude-code-client-metadata")]
    [TestCase("ftp://claude.ai/oauth/claude-code-client-metadata")]
    [TestCase("claude.ai/oauth/claude-code-client-metadata")]
    [TestCase("not a url")]
    public async Task Fetch_WhenNotHttps_ShouldReturnNull(string clientId)
    {
        (await Documents.FetchAsync(clientId)).ShouldBeNull();
    }

    [TestCase("https://claude.ai")]
    [TestCase("https://claude.ai/")]
    public async Task Fetch_WhenNoPath_ShouldReturnNull(string clientId)
    {
        (await Documents.FetchAsync(clientId)).ShouldBeNull();
    }

    [Test]
    public async Task Fetch_WhenKnownDocument_ShouldReturnMetadataClientWithHost()
    {
        var document = await Documents.FetchAsync(KnownDocumentUrl);

        document.ShouldNotBeNull();
        document.Client.ClientId.ShouldBe(KnownDocumentUrl);
        document.Client.Kind.ShouldBe(OAuthClientKind.MetadataDocument);
        document.Client.ClientHost.ShouldBe(new Uri(KnownDocumentUrl).Host);
        document.Client.ClientName.ShouldNotBeNullOrWhiteSpace();
        document.Client.RedirectUris.ShouldNotBeEmpty();
        document.Client.Verified.ShouldBeTrue();
    }
}
