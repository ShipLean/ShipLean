namespace ShipLean.Contracts.Links;

// Reads an address for a link card. The card service uses the first source (lowest Order) that CanHandleAsync the address, and
// builds a fallback card from the address alone when none does. A source never throws for an address it can't read: it
// answers with PreviewStatus Unreachable or Private.
[MultipleImplementations]
public interface ILinkCardSource
{
    int Order { get; }

    // Async because a source may read a setting to decide (the GitLab source answers from the GitLab URL setting).
    Task<bool> CanHandleAsync(Uri url, CancellationToken cancellationToken);

    Task<LinkCardFetch> FetchAsync(Uri url, CancellationToken cancellationToken);
}
