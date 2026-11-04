using System.Collections.Concurrent;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// U2 is in U1's wave, so the upload's tests stand this in for IDemoDelivery. It reads the video through, as a real delivery must, keeps what it was
// handed, and runs Before first (a test makes it wait, refuse or throw there).
internal sealed class RecordingDemoDelivery : IDemoDelivery
{
    private readonly ConcurrentQueue<Delivered> _calls = new();
    private int _entered;

    public sealed record Delivered(Guid ItemId, string Token, DemoChaptersJson Chapters, string Script, byte[] Video);

    // Deliveries that read the whole video.
    public IReadOnlyList<Delivered> Calls => [.. _calls];

    // Every time DeliverAsync was called, whether or not it went on to read the video.
    public int Entered => Volatile.Read(ref _entered);

    public Func<DemoDeliveryParts, Task>? Before { get; set; }

    // Who ICurrentUser named when the last call came in (set by ActingDemoDelivery).
    public Guid? ActingUserId { get; set; }

    public void Reset()
    {
        _calls.Clear();
        Volatile.Write(ref _entered, 0);
        Before = null;
        ActingUserId = null;
    }

    public async Task<DemoUploadedDto> DeliverAsync(DemoDeliveryParts parts, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _entered);
        if (Before is { } before)
        {
            await before(parts);
        }

        using var video = new MemoryStream();
        await parts.Video.CopyToAsync(video, cancellationToken);
        _calls.Enqueue(new Delivered(parts.Grant.WorkItemId, parts.Token, parts.Chapters, parts.Script, video.ToArray()));
        return new DemoUploadedDto("Sprint 23", Guid.NewGuid(), video.Length, parts.Chapters.Chapters.Count, "https://planner.example.com/open");
    }
}
