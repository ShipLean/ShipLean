using System.Collections.Concurrent;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.TestSupport.IssueIntake;

// An IImagePublications that answers what the test sets for a commit (by sha), and Unknown for any other.
public sealed class FixedImagePublications : IImagePublications
{
    private readonly ConcurrentDictionary<string, ImagePublication> _answers = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string sha, ImagePublication publication) => _answers[sha] = publication;

    public Task<ImagePublication> FindAsync(IssueRef repository, string sha, CancellationToken cancellationToken = default)
        => Task.FromResult(_answers.TryGetValue(sha, out var answer) ? answer : new ImagePublication.Unknown());
}
