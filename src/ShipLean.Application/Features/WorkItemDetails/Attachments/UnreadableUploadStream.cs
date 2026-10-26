using ShipLean.Application.Uploads;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// Reads the part of a multipart body that holds the file and turns a body that ends early into a validation error. Only reads
// are wrapped: a failure while the store writes the bytes is not the sender's mistake and passes through.
internal sealed class UnreadableUploadStream(Stream inner) : ForwardOnlyStream
{
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.ReadAsync(buffer, cancellationToken);
        }
        catch (IOException error) when (MultipartUpload.IsTruncation(error))
        {
            throw MultipartUpload.Unreadable();
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
}
