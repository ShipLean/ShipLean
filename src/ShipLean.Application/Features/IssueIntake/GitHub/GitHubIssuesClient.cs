using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ShipLean.Application.Features.Links.GitHub;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.IssueIntake.GitHub;

// IGitHubIssues over the GitHub REST API (GitHubApiUrl), with the instance token as a bearer. It uses the github-source client
// (pinned, address-checked connector, no redirects), so the token only goes to the configured https API address.
// Results (GitHub's documented statuses): 401, 403, 404, 410, 422 and other 4xx are Failed, except a rate-limited 403
// (x-ratelimit-remaining 0, or retry-after) which is Retry, as is 429. A 5xx is Retry, except on a POST, where GitHub may
// already have acted on the body, so it is Ambiguous; the same goes for a timeout. A connection that never opened is Retry.
public sealed class GitHubIssuesClient(IHttpClientFactory clients, IIntegrationSettings settings) : IGitHubIssues
{
    // The largest list page body read (100 comments of up to 65,536 characters each are about 6.5 MB at worst).
    private const int MaxListBody = 16 * 1024 * 1024;
    private const int PageSize = 100;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<GitHubWriteResult> CommentAsync(IssueRef issue, string body, CancellationToken cancellationToken = default)
    {
        var sent = await SendAsync(HttpMethod.Post, IssuePath(issue, "/comments"), new { body }, cancellationToken);
        if (sent.Result is not GitHubWriteResult.Ok)
        {
            return sent.Result;
        }

        using var response = sent.Response!;
        var created = await ReadAsync<CommentReply>(response.Content, GitHubLimits.MaxBody, cancellationToken);
        // GitHub answered 2xx, so the comment exists; without its id the outbox marker is how a retry finds it.
        return created is null ? new GitHubWriteResult.Ambiguous("unreadableReply") : new GitHubWriteResult.Ok(created.Id);
    }

    public Task<GitHubWriteResult> CloseAsync(IssueRef issue, CloseReason reason, CancellationToken cancellationToken = default)
        => PatchStateAsync(issue, "closed", reason == CloseReason.Completed ? "completed" : "not_planned", cancellationToken);

    public Task<GitHubWriteResult> ReopenAsync(IssueRef issue, CancellationToken cancellationToken = default)
        => PatchStateAsync(issue, "open", "reopened", cancellationToken);

    public async Task<GitHubCommentList> ListRecentCommentsAsync(IssueRef issue, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var sinceText = since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var comments = new List<GitHubComment>();
        for (var page = 1; page <= IGitHubIssues.MaxListedComments / PageSize; page++)
        {
            var sent = await SendAsync(HttpMethod.Get, IssuePath(issue, $"/comments?since={Uri.EscapeDataString(sinceText)}&per_page={PageSize}&page={page}"), null, cancellationToken);
            switch (sent.Result)
            {
                case GitHubWriteResult.Failed failed:
                    return new GitHubCommentList.Failed(failed.Error);
                case GitHubWriteResult.Retry or GitHubWriteResult.Ambiguous:
                    return new GitHubCommentList.Retry(sent.Result is GitHubWriteResult.Retry retry ? retry.Error : "ambiguous");
            }

            using var response = sent.Response!;
            var replies = await ReadAsync<List<CommentReply>>(response.Content, MaxListBody, cancellationToken);
            if (replies is null)
            {
                return new GitHubCommentList.Failed("unreadableReply");
            }

            comments.AddRange(replies.Select(r => new GitHubComment(r.Id, r.User?.Login, r.Body ?? "", r.CreatedAt)));
            if (replies.Count < PageSize)
            {
                return new GitHubCommentList.Listed(comments, Complete: true);
            }
        }

        return new GitHubCommentList.Listed(comments, Complete: false);
    }

    // GET /user: the login the instance token belongs to. Throws when there is no token or GitHub doesn't answer with one.
    public async Task<string> FetchSelfLoginAsync(CancellationToken cancellationToken = default)
    {
        var sent = await SendAsync(HttpMethod.Get, "user", null, cancellationToken);
        if (sent.Result is not GitHubWriteResult.Ok)
        {
            throw new InvalidOperationException($"GitHub did not say who the instance token belongs to: {Describe(sent.Result)}.");
        }

        using var response = sent.Response!;
        var user = await ReadAsync<UserReply>(response.Content, GitHubLimits.MaxBody, cancellationToken);
        return string.IsNullOrEmpty(user?.Login)
            ? throw new InvalidOperationException("GitHub's /user answer has no login.")
            : user.Login;
    }

    private async Task<GitHubWriteResult> PatchStateAsync(IssueRef issue, string state, string stateReason, CancellationToken cancellationToken)
    {
        var sent = await SendAsync(HttpMethod.Patch, IssuePath(issue, ""), new { state, stateReason }, cancellationToken);
        sent.Response?.Dispose();
        return sent.Result is GitHubWriteResult.Ok ? new GitHubWriteResult.Ok(null) : sent.Result;
    }

    private static string IssuePath(IssueRef issue, string rest)
    {
        if (issue.Host != IssueRef.GitHubHost || issue.Repository.Split('/') is not [var owner, var name] || owner.Length == 0 || name.Length == 0)
        {
            throw new ArgumentException($"Not a github.com issue: {issue}.", nameof(issue));
        }

        return $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/issues/{issue.Number.ToString(CultureInfo.InvariantCulture)}{rest}";
    }

    // Sends one request. Result Ok comes with the (undisposed) response; any other result has none.
    private async Task<(GitHubWriteResult Result, HttpResponseMessage? Response)> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync(cancellationToken);
        if (string.IsNullOrEmpty(config.GitHubToken))
        {
            return (new GitHubWriteResult.Failed("noToken"), null);
        }

        if (!Uri.TryCreate($"{config.GitHubApiUrl.TrimEnd('/')}/{path}", UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
        {
            return (new GitHubWriteResult.Failed("apiUrlNotHttps"), null);
        }

        var isPost = method == HttpMethod.Post;
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ShipLean", "1"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Wire);
        }

        HttpResponseMessage response;
        try
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.GitHubToken);
            response = await clients.CreateClient(GitHubSource.HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (isPost ? new GitHubWriteResult.Ambiguous("timeout") : new GitHubWriteResult.Retry("timeout"), null);
        }
        catch (HttpRequestException exception)
        {
            var neverSent = exception.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError or HttpRequestError.SecureConnectionError;
            return (isPost && !neverSent ? new GitHubWriteResult.Ambiguous(exception.HttpRequestError.ToString()) : new GitHubWriteResult.Retry(exception.HttpRequestError.ToString()), null);
        }
        // FormatException: a token the header rules refuse. Its message quotes the value, so it is not kept.
        catch (FormatException)
        {
            return (new GitHubWriteResult.Failed("badToken"), null);
        }

        var result = Classify(response, isPost);
        if (result is GitHubWriteResult.Ok)
        {
            return (result, response);
        }

        response.Dispose();
        return (result, null);
    }

    private static GitHubWriteResult Classify(HttpResponseMessage response, bool isPost)
    {
        var status = (int)response.StatusCode;
        var error = status.ToString(CultureInfo.InvariantCulture);
        if (response.IsSuccessStatusCode)
        {
            return new GitHubWriteResult.Ok(null);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests || (response.StatusCode == HttpStatusCode.Forbidden && IsRateLimited(response)))
        {
            return new GitHubWriteResult.Retry(error);
        }

        if (status >= 500)
        {
            return isPost ? new GitHubWriteResult.Ambiguous(error) : new GitHubWriteResult.Retry(error);
        }

        return new GitHubWriteResult.Failed(error);
    }

    // GitHub's rate limits answer 403 or 429; a 403 is a rate limit when x-ratelimit-remaining is 0 or retry-after is set.
    private static bool IsRateLimited(HttpResponseMessage response)
        => response.Headers.RetryAfter is not null
           || (response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) && remaining.FirstOrDefault() == "0");

    private static string Describe(GitHubWriteResult result) => result switch
    {
        GitHubWriteResult.Retry retry => retry.Error,
        GitHubWriteResult.Ambiguous ambiguous => ambiguous.Error,
        GitHubWriteResult.Failed failed => failed.Error,
        _ => "ok",
    };

    // The body parsed as T, or null when it is larger than max or isn't T's JSON.
    private static async Task<T?> ReadAsync<T>(HttpContent content, int max, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk.AsSpan(0, read));
            if (buffer.Length > max)
            {
                return default;
            }
        }

        buffer.Position = 0;
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(buffer, Wire, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private sealed record CommentReply(long Id, UserReply? User, string? Body, DateTimeOffset CreatedAt);

    private sealed record UserReply(string? Login);
}
