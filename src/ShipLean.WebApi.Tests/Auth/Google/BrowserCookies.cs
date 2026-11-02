namespace ShipLean.WebApi.Tests.Auth.Google;

// A cookie jar that keeps what a browser keeps. CookieContainer refuses a cookie whose Path isn't a prefix of the request
// that set it, and the handler sets its correlation and nonce cookies from /start for the callback's path. Every cookie
// goes back on every request, which is enough here: each test talks to one host, and the Path only ever narrows.
public sealed class BrowserCookies() : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
{
    private readonly Dictionary<string, string> _cookies = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        }
        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var value in values)
            {
                Remember(value);
            }
        }
        return response;
    }

    private void Remember(string setCookie)
    {
        var pair = setCookie.Split(';', 2)[0].Split('=', 2);
        // The handler ends a cookie with a past expiry and an empty value.
        if (pair[1].Length == 0 || setCookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
        {
            _cookies.Remove(pair[0]);
        }
        else
        {
            _cookies[pair[0]] = pair[1];
        }
    }
}
