using System.Net.Http.Json;
using System.Text.Json;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.GitLab;

// Link cards for merge requests and issues on the configured GitLab (GitLabUrl), read from its v4 API with the GitLabToken as
// PRIVATE-TOKEN when one is set and the GitLab address is https. It handles nothing until GitLabUrl is set. Any failure of the API call is an Unreachable
// card carrying what the address itself says.
public sealed class GitLabSource(IHttpClientFactory clients, IIntegrationSettings settings) : ILinkCardSource
{
    public const string HttpClientName = "gitlab-source";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public int Order => 20;

    public async Task<bool> CanHandleAsync(Uri url, CancellationToken cancellationToken)
        => GitLabUrl.TryParse(url, (await settings.GetAsync(cancellationToken)).GitLabUrl, out _);

    public async Task<LinkCardFetch> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync(cancellationToken);
        if (!GitLabUrl.TryParse(url, config.GitLabUrl, out var address))
        {
            throw new ArgumentException("Not a merge request or issue address on the configured GitLab.", nameof(url));
        }

        var unreachable = new LinkCardFetch(address.Kind, null, address.Number, address.Location, null, null, null, null, null, LinkPreviewStatus.Unreachable);
        if (!Uri.TryCreate(string.Join('/', config.GitLabUrl!.Trim().TrimEnd('/'), address.ApiPath), UriKind.Absolute, out var apiUrl) || apiUrl.Scheme is not ("http" or "https"))
        {
            return unreachable;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            if (!string.IsNullOrEmpty(config.GitLabToken) && apiUrl.Scheme == Uri.UriSchemeHttps)
            {
                request.Headers.Add("PRIVATE-TOKEN", config.GitLabToken);
            }

            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return unreachable;
            }

            var reply = await GitLabLimits.ReadAsync<GitLabReply>(response.Content, Wire, cancellationToken);
            return reply is null || string.IsNullOrWhiteSpace(reply.Title)
                ? unreachable
                : unreachable with
                {
                    Title = GitLabLimits.Truncate(reply.Title, GitLabLimits.MaxTitle),
                    Author = GitLabLimits.Truncate(reply.Author?.Username, GitLabLimits.MaxAuthor),
                    State = GitLabStates.Of(reply),
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
