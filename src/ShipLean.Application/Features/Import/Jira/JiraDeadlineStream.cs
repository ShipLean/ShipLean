using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Import.Jira;

// A download that cannot stall: each read gets the deadline, so a site that stops sending (or drips one byte at a time with long gaps)
// fails the read instead of holding the connection. The total size is the reader's to cap. Disposing it releases the response.
public sealed class JiraDeadlineStream(HttpResponseMessage response, Stream body, TimeSpan deadline) : AsyncReadStream
{
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(deadline);
        try
        {
            return await body.ReadAsync(buffer, limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw ApiException.NotConfigured("Jira took too long to send the attachment.");
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            throw ApiException.NotConfigured("Jira did not finish sending the attachment.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            body.Dispose();
            response.Dispose();
        }
        base.Dispose(disposing);
    }
}
