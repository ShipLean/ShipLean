using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.GitHub;

// TODO: temporary; the real image publications (the docker-publish.yml runs from the GitHub Actions API, with the compare API
// for containment) replace this class and its registration. Until then every commit's publication is Unknown.
public sealed class UnknownImagePublications : IImagePublications
{
    public Task<ImagePublication> FindAsync(IssueRef repository, string sha, CancellationToken cancellationToken = default)
        => Task.FromResult<ImagePublication>(new ImagePublication.Unknown());
}
