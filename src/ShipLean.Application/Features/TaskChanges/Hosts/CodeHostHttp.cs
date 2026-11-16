using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// One GET to a code host. Status is the HTTP status, or 0 when the call failed on the network, timed out, was refused by the
// header rules (a token they won't take) or came back larger than MaxBody. Body is read only on a success status.
internal sealed record CodeHostReply(int Status, string? Body, string? NextPage)
{
    public const int MaxBody = 8 * 1024 * 1024;

    public bool Ok => Status is >= 200 and < 300;

    public static async Task<CodeHostReply> GetAsync(HttpClient client, Uri url, Action<HttpRequestMessage> decorate, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ShipLean", "1"));
            decorate(request);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                return new CodeHostReply(status, null, null);
            }

            var next = response.Headers.TryGetValues("x-next-page", out var values) ? values.FirstOrDefault() : null;
            var body = await ReadAsync(response.Content, cancellationToken);
            return body is null ? new CodeHostReply(0, null, null) : new CodeHostReply(status, body, next);
        }
        // FormatException: a token the header rules refuse. Its message quotes the value, so it is dropped here, not logged.
        catch (Exception exception) when (exception is HttpRequestException or IOException or NotSupportedException or FormatException
                                          || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new CodeHostReply(0, null, null);
        }
    }

    // The body as text, or null when it is over MaxBody.
    private static async Task<string?> ReadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk.AsSpan(0, read));
            if (buffer.Length > MaxBody)
            {
                return null;
            }
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    // The answer for a read that did not succeed. RepositoryStatus is the repository's own status, asked for when the host
    // answers 404 for a missing repository and a missing commit alike: only a repository that answers can have a missing commit.
    public static CommitReadStatus FailureOf(int status, int? repositoryStatus)
        => status switch
        {
            (int)HttpStatusCode.NotFound => repositoryStatus is >= 200 and < 300 ? CommitReadStatus.Missing : CommitReadStatus.Unreadable,
            (int)HttpStatusCode.UnprocessableEntity => CommitReadStatus.Missing,
            _ => CommitReadStatus.Unreadable,
        };
}
