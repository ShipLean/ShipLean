using System.Net;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.TestSupport.Hosting.Proxy;

// A browser behind the identity proxy, for proxy tests: its own cookie jar, no redirects followed, the client header
// and JSON bodies of ApiClient, and identity headers sent on every request until changed. WithIdentity, WithToken and
// WithoutIdentity each replace the identity, so one client can sign in as Mei and then arrive as Priya.
public sealed class ProxyApiClient : IDisposable
{
    private readonly IdentityHeaders _identity;

    public ProxyApiClient(Uri baseAddress)
    {
        Cookies = new CookieContainer();
        _identity = new IdentityHeaders(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = Cookies, UseCookies = true });
        Http = new HttpClient(_identity) { BaseAddress = baseAddress };
        Api = new ApiClient(Http);
    }

    public CookieContainer Cookies { get; }

    public HttpClient Http { get; }

    public ApiClient Api { get; }

    // The jar's cookie of that name for the base address, or null.
    public Cookie? Cookie(string name) => Cookies.GetCookies(Http.BaseAddress!)[name];

    // The fake reader's headers (FakeProxyIdentityReader). A null name sends no name header.
    public ProxyApiClient WithIdentity(string email, string? name = null)
    {
        var headers = new List<(string, string)> { (FakeProxyIdentityReader.EmailHeader, email) };
        if (name != null)
        {
            headers.Add((FakeProxyIdentityReader.NameHeader, name));
        }
        return WithHeaders([.. headers]);
    }

    // A minted token in the configured token header. For "Authorization" it goes as "Bearer <token>".
    public ProxyApiClient WithToken(string header, string token)
        => WithHeaders((header, string.Equals(header, "Authorization", StringComparison.OrdinalIgnoreCase) ? $"Bearer {token}" : token));

    // Any identity headers, such as plain-header mode's Remote-Email and Remote-Name.
    public ProxyApiClient WithHeaders(params (string Name, string Value)[] headers)
    {
        _identity.Headers = headers;
        return this;
    }

    public ProxyApiClient WithoutIdentity() => WithHeaders();

    public Task<HttpResponseMessage> GetAsync(string path) => Api.GetAsync(path);

    public Task<T> GetJsonAsync<T>(string path) => Api.GetJsonAsync<T>(path);

    public Task<HttpResponseMessage> PostJsonAsync(string path, object? body = null) => Api.PostJsonAsync(path, body);

    public Task<HttpResponseMessage> PatchJsonAsync(string path, object? body = null) => Api.PatchJsonAsync(path, body);

    public Task<HttpResponseMessage> PutJsonAsync(string path, object? body = null) => Api.PutJsonAsync(path, body);

    public Task<HttpResponseMessage> DeleteAsync(string path) => Api.DeleteAsync(path);

    public void Dispose() => Http.Dispose();

    private sealed class IdentityHeaders(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public IReadOnlyList<(string Name, string Value)> Headers { get; set; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            foreach (var (name, value) in Headers)
            {
                request.Headers.Remove(name);
                request.Headers.TryAddWithoutValidation(name, value);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
