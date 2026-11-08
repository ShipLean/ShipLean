using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;

namespace ShipLean.WebApi.Tests.IssueIntake.Journeys;

// An IHttpClientFactory whose clients play GitHub's REST API for one repository: comments get ids from nextId, PATCH on an issue
// is a state change, GET /user is selfLogin, a comment list returns every comment on the issue, and Respond sets other GET answers
// by path (404 for any other). PlanNextComment makes the next comment POST on an issue fail the way GitHub or the network can.
// The loopback HttpsStubServer can't serve these paths (it skips paths whose last segment has a dot).
internal sealed partial class FakeGitHub(string selfLogin, Func<long> nextId) : IHttpClientFactory
{
    private string _repoPath = "/repos/acme/happy";
    private readonly ConcurrentQueue<(int Number, string Kind, string Body)> _writes = new();
    private readonly ConcurrentQueue<Listed> _listed = new();
    private readonly ConcurrentQueue<int> _posts = new();
    private readonly ConcurrentDictionary<int, ConcurrentQueue<CommentAnswer>> _plans = new();
    private readonly ConcurrentDictionary<string, string> _answers = new(StringComparer.Ordinal);

    public string SelfLogin => selfLogin;

    public void Reset(string repoPath)
    {
        _repoPath = repoPath;
        _writes.Clear();
        _listed.Clear();
        _posts.Clear();
        _plans.Clear();
        _answers.Clear();
    }

    public void Respond(string path, string body) => _answers[path] = body;

    // The next comment POST on the issue gets this answer instead of 201.
    public void PlanNextComment(int number, CommentAnswer answer) => _plans.GetOrAdd(number, _ => new()).Enqueue(answer);

    // The comment bodies on the issue that ShipLean posted and that landed, in order, without the outbox marker.
    public IReadOnlyList<string> Comments(int number) => [.. _writes.Where(w => w.Number == number && w.Kind == "comment").Select(w => w.Body)];

    // Every write for the issue in the order GitHub received it: the kind ("comment" or "state") and the body.
    public IReadOnlyList<(string Kind, string Body)> Writes(int number) => [.. _writes.Where(w => w.Number == number).Select(w => (w.Kind, w.Body))];

    // The state changes sent for the issue, as "closed completed".
    public IReadOnlyList<string> Closes(int number) => [.. _writes.Where(w => w.Number == number && w.Kind == "state").Select(w => w.Body)];

    // How many comment POSTs reached the issue, landed or not.
    public int Posts(int number) => _posts.Count(n => n == number);

    // Every comment a list of the issue returns, by anyone, with its full body.
    public IReadOnlyList<Listed> Listing(int number) => [.. _listed.Where(c => c.Number == number)];

    public HttpClient CreateClient(string name) => new(new Answerer(this));

    [GeneratedRegex(@"\n\n<!-- shiplean:outbox:[0-9a-f-]{36} -->$", RegexOptions.None, 1000)]
    private static partial Regex Marker();

    private void Land(int number, string login, string body, long id)
    {
        _listed.Enqueue(new Listed(number, id, login, body));
        if (string.Equals(login, selfLogin, StringComparison.Ordinal) && Marker().Match(body) is { Success: true } marker)
        {
            _writes.Enqueue((number, "comment", body[..marker.Index]));
        }
    }

    // A comment on the fake issue: its id, author and the body as GitHub keeps it.
    public sealed record Listed(int Number, long Id, string Login, string Body);

    // How the next comment POST on an issue goes. Status null: the request times out. Lands: the comment is on the issue all the
    // same. RateLimitRemaining: GitHub's x-ratelimit-remaining header on the answer. Plant: a comment by someone else (login and
    // body, made from the posted body) that appears on the issue meanwhile.
    public sealed record CommentAnswer(HttpStatusCode? Status, bool Lands = false, string? RateLimitRemaining = null, Func<string, (string Login, string Body)>? Plant = null);

    private sealed class Answerer(FakeGitHub owner) : HttpMessageHandler
    {
        private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var isIssue = path.StartsWith($"{owner._repoPath}/issues/", StringComparison.Ordinal);
            var number = isIssue ? int.Parse(segments[4], CultureInfo.InvariantCulture) : 0;
            if (request.Method == HttpMethod.Post && isIssue && segments.Length == 6 && segments[5] == "comments")
            {
                var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.GetProperty("body").GetString()!;
                // The deliverer adds the outbox marker at send time; tests compare the text before it.
                Marker().IsMatch(body).ShouldBeTrue("a posted comment carries the outbox marker");
                owner._posts.Enqueue(number);
                return Post(number, body);
            }

            if (request.Method == HttpMethod.Patch && isIssue && segments.Length == 5)
            {
                var state = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement;
                owner._writes.Enqueue((number, "state", $"{state.GetProperty("state").GetString()} {state.GetProperty("state_reason").GetString()}"));
                return Json(HttpStatusCode.OK, "{}");
            }

            if (request.Method == HttpMethod.Get && isIssue && segments.Length == 6 && segments[5] == "comments")
            {
                var listed = owner.Listing(number).Select(c => new { id = c.Id, user = new { login = c.Login }, body = c.Body });
                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(listed));
            }

            if (request.Method == HttpMethod.Get && path == "/user")
            {
                return Json(HttpStatusCode.OK, $$"""{ "login": "{{owner.SelfLogin}}" }""");
            }

            return request.Method == HttpMethod.Get && owner._answers.TryGetValue(path, out var answer)
                ? Json(HttpStatusCode.OK, answer)
                : Json(HttpStatusCode.NotFound, """{ "message": "Not Found" }""");
        }

        private HttpResponseMessage Post(int number, string body)
        {
            if (!owner._plans.TryGetValue(number, out var plans) || !plans.TryDequeue(out var plan))
            {
                var id = owner.NextCommentId();
                owner.Land(number, owner.SelfLogin, body, id);
                return Json(HttpStatusCode.Created, $$"""{ "id": {{id}} }""");
            }

            if (plan.Plant is { } plant)
            {
                var (login, planted) = plant(body);
                owner.Land(number, login, planted, owner.NextCommentId());
            }

            if (plan.Lands)
            {
                owner.Land(number, owner.SelfLogin, body, owner.NextCommentId());
            }

            if (plan.Status is not { } status)
            {
                // HttpClient reports its own timeout as a TaskCanceledException while the caller's token is not cancelled.
                throw new TaskCanceledException("The request timed out.");
            }

            var response = Json(status, """{ "message": "planned failure" }""");
            if (plan.RateLimitRemaining is { } remaining)
            {
                response.Headers.Add("x-ratelimit-remaining", remaining);
            }

            return response;
        }
    }

    private long NextCommentId() => nextId();
}
