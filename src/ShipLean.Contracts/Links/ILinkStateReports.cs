namespace ShipLean.Contracts.Links;

// What an agent says about an address it can see and ShipLean can't (a private GitLab, say). Applies to every live link with
// that URL in the projects the current user can access, and stamps the reporting agent.
public interface ILinkStateReports
{
    // Title and State are applied when given. Throws NotFound when no such link matches.
    Task ReportAsync(string url, string? title, LinkState? state, DateTimeOffset asOf, CancellationToken cancellationToken = default);
}
