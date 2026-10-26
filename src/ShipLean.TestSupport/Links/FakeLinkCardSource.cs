using System.Collections.Concurrent;
using ShipLean.Contracts.Links;

namespace ShipLean.TestSupport.Links;

// An ILinkCardSource with no network. It handles every address on Host (default "fake.example"), answers what a test
// registered for the exact address, and a plain reachable page for any other address on its host. Register it in a test
// with ConfigureTestServices (services.AddSingleton<ILinkCardSource>(fake)). Fetched lists the addresses it was asked for.
public sealed class FakeLinkCardSource(int order = 50, string host = "fake.example") : ILinkCardSource
{
    private readonly ConcurrentDictionary<string, LinkCardFetch> _responses = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _fetched = new();

    public int Order => order;

    public string Host => host;

    public IReadOnlyList<string> Fetched => [.. _fetched];

    // A fetch the source gives for this exact address from now on.
    public void Respond(string url, LinkCardFetch fetch) => _responses[new Uri(url).AbsoluteUri] = fetch;

    public static LinkCardFetch ReachablePage(string title) => new(LinkKind.Page, title, null, null, null, null, null, null, null, LinkPreviewStatus.Ok);

    public static LinkCardFetch Unreachable() => new(LinkKind.Page, null, null, null, null, null, null, null, null, LinkPreviewStatus.Unreachable);

    public Task<bool> CanHandleAsync(Uri url, CancellationToken cancellationToken) => Task.FromResult(url.Host.Equals(host, StringComparison.OrdinalIgnoreCase));

    public Task<LinkCardFetch> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        _fetched.Enqueue(url.AbsoluteUri);
        return Task.FromResult(_responses.TryGetValue(url.AbsoluteUri, out var fetch) ? fetch : ReachablePage("Fake page"));
    }
}
