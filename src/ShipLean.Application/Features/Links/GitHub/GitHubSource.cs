using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.GitHub;

// Link cards for github.com pull requests and issues, read from the GitHub REST API (GitHubApiUrl, with the GitHubToken as a
// bearer when one is set and the API address is https). Any failure of the API call is an Unreachable card carrying what the address itself says.
public sealed class GitHubSource(IHttpClientFactory clients, IIntegrationSettings settings) : ILinkCardSource
{
    public const string HttpClientName = "github-source";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public int Order => 10;

    public Task<bool> CanHandleAsync(Uri url, CancellationToken cancellationToken) => Task.FromResult(GitHubUrl.TryParse(url, out _));

    public async Task<LinkCardFetch> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!GitHubUrl.TryParse(url, out var address))
        {
            throw new ArgumentException("Not a GitHub pull request or issue address.", nameof(url));
        }

        var unreachable = new LinkCardFetch(address.Kind, null, address.Number, address.Location, null, null, null, null, null, LinkPreviewStatus.Unreachable);
        var config = await settings.GetAsync(cancellationToken);
        if (!Uri.TryCreate(string.Join('/', config.GitHubApiUrl.TrimEnd('/'), address.ApiPath), UriKind.Absolute, out var apiUrl) || apiUrl.Scheme is not ("http" or "https"))
        {
            return unreachable;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ShipLean", "1"));
            if (!string.IsNullOrEmpty(config.GitHubToken) && apiUrl.Scheme == Uri.UriSchemeHttps)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.GitHubToken);
            }

            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return unreachable;
            }

            var reply = await GitHubLimits.ReadAsync<GitHubReply>(response.Content, Wire, cancellationToken);
            return reply is null || string.IsNullOrWhiteSpace(reply.Title)
                ? unreachable
                : unreachable with
                {
                    Title = GitHubLimits.Truncate(reply.Title, GitHubLimits.MaxTitle),
                    Author = GitHubLimits.Truncate(reply.User?.Login, GitHubLimits.MaxAuthor),
                    State = GitHubStates.Of(reply),
                    PreviewStatus = LinkPreviewStatus.Ok,
                };
        }
        // FormatException: a token the header rules refuse. Its message quotes the value, so it is dropped here, not logged.
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or NotSupportedException or FormatException
                                          || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return unreachable;
        }
    }
}
