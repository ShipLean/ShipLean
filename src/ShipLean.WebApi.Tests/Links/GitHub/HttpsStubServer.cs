using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace ShipLean.WebApi.Tests.Links.GitHub;

// A StubHttpServer over https on a loopback port with a throwaway self-signed certificate, for tests of what a source sends
// to an https API (a token goes only there). Callers' clients accept any certificate (RecordingHttpClientFactory does).
// Answers a fixed body per path; other paths 404. Every request is recorded as "METHOD /path".
public sealed class HttpsStubServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly X509Certificate2 _certificate;
    private readonly ConcurrentDictionary<string, (int Status, byte[] Body)> _responses = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _requests = new();

    private HttpsStubServer(WebApplication app, X509Certificate2 certificate, Uri baseAddress)
    {
        _app = app;
        _certificate = certificate;
        BaseAddress = baseAddress;
    }

    // "https://127.0.0.1:port/".
    public Uri BaseAddress { get; }

    public IReadOnlyList<string> Requests => [.. _requests];

    public static async Task<HttpsStubServer> StartAsync()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(TimeProvider.System.GetUtcNow().AddDays(-1), TimeProvider.System.GetUtcNow().AddDays(1));
        var certificate = X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        builder.Logging.ClearProviders();
        var app = builder.Build();
        HttpsStubServer? server = null;
        app.MapFallback((HttpRequest http, HttpResponse response, CancellationToken cancellationToken) => server!.AnswerAsync(http, response, cancellationToken));
        await app.StartAsync();
        server = new HttpsStubServer(app, certificate, new Uri(app.Urls.First()));
        return server;
    }

    public void Respond(string path, string body, int status = StatusCodes.Status200OK) => _responses[path] = (status, Encoding.UTF8.GetBytes(body));

    private async Task AnswerAsync(HttpRequest request, HttpResponse response, CancellationToken cancellationToken)
    {
        _requests.Enqueue($"{request.Method} {request.Path.Value}");
        if (!_responses.TryGetValue(request.Path.Value ?? "/", out var answer))
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        response.StatusCode = answer.Status;
        response.ContentType = "application/json";
        await response.Body.WriteAsync(answer.Body, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _certificate.Dispose();
    }
}
