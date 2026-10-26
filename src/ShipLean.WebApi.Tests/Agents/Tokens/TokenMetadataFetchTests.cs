using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Agents.Tokens;

// A token request names its client only to the stores, which bind it to the authorization. An anonymous caller must not be
// able to make the server fetch a metadata document (or register a client) by sending an https client_id.
[TestFixture]
public class TokenMetadataFetchTests : TokenFixture
{
    private const string Url = "https://unknown.example.test/client.json";
    private readonly CountingDocuments _documents = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IClientMetadataDocuments>(_documents);

    [Test]
    public async Task TokenAndRevoke_WhenClientIsAnUnknownUrl_ShouldNeverFetchItsDocument()
    {
        var (code, codeBody) = await TokenAsync(CodeGrant(Url, "c"));
        var (refresh, refreshBody) = await TokenAsync(RefreshGrant(Url, "r"));
        var (revoke, _) = await PostAsync("/oauth/revoke", new() { ["token"] = "t", ["client_id"] = Url });

        RefusalOf(code, codeBody).ShouldBe("invalid_grant");
        RefusalOf(refresh, refreshBody).ShouldBe("invalid_grant");
        revoke.StatusCode.ShouldBe(HttpStatusCode.OK);
        _documents.Calls.ShouldBe(0);
    }

    [Test]
    public async Task FindAsync_WhenClientIsAnUnknownUrl_ShouldCountOneFetch()
    {
        await using var scope = Host.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IOAuthClients>().FindAsync(Url);

        _documents.Calls.ShouldBe(1);
        _documents.Reset();
    }

    private sealed class CountingDocuments : IClientMetadataDocuments
    {
        private int _calls;

        public int Calls => _calls;

        public void Reset() => Interlocked.Exchange(ref _calls, 0);

        public Task<ClientMetadataDocument?> FetchAsync(string clientId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult<ClientMetadataDocument?>(null);
        }
    }
}
