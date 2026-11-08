namespace ShipLean.Contracts.IssueIntake;

// Whether a commit has been published as an image (D14): the first successful docker-publish.yml run on the default branch whose
// commit contains `sha`. Only the repository of `repository` is used; its Number is ignored.
public interface IImagePublications
{
    Task<ImagePublication> FindAsync(IssueRef repository, string sha, CancellationToken cancellationToken = default);
}
