using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.TestSupport.Http;

// A real HTTP server on a loopback port that answers fixed responses per path, for tests of code that reads web pages and
// provider APIs: no external network. Unregistered paths answer 404. Every request is recorded as "METHOD /path?query".
// The address is loopback, so the SSRF guard (IAddressCheck) refuses it: a test of a source swaps in an IAddressCheck that
// allows loopback.
public sealed class StubHttpServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentDictionary<string, StubResponse> _responses = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _requests = new();

    private StubHttpServer(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    // "http://127.0.0.1:port/".
    public Uri BaseAddress { get; }

    public IReadOnlyList<string> Requests => [.. _requests];

    public static async Task<StubHttpServer> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        StubHttpServer? server = null;
        app.Map("{**path}", (HttpRequest request, HttpResponse response, CancellationToken cancellationToken) => server!.AnswerAsync(request, response, cancellationToken));
        await app.StartAsync();
        server = new StubHttpServer(app, new Uri(app.Urls.First()));
        return server;
    }

    // Answers GET and HEAD requests for the path (no query) with the body.
    public void Respond(string path, string body, string contentType = "text/html; charset=utf-8", int status = StatusCodes.Status200OK, IReadOnlyDictionary<string, string>? headers = null)
        => Respond(path, Encoding.UTF8.GetBytes(body), contentType, status, headers);

    public void Respond(string path, byte[] body, string contentType, int status = StatusCodes.Status200OK, IReadOnlyDictionary<string, string>? headers = null)
        => _responses[path] = new StubResponse(status, body, contentType, headers);

    // The address of a path on this server.
    public Uri UrlOf(string path) => new(BaseAddress, path);

    private async Task AnswerAsync(HttpRequest request, HttpResponse response, CancellationToken cancellationToken)
    {
        _requests.Enqueue($"{request.Method} {request.GetEncodedPathAndQuery()}");
        if (!_responses.TryGetValue(request.Path.Value ?? "/", out var answer))
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        response.StatusCode = answer.Status;
        response.ContentType = answer.ContentType;
        foreach (var (name, value) in answer.Headers ?? new Dictionary<string, string>())
        {
            response.Headers[name] = value;
        }

        await response.Body.WriteAsync(answer.Body, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
