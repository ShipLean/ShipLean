using System.Net;
using System.Text.Json.Nodes;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.LinearApi;

// The reads, with the documents @linear/sdk sends verbatim (Fixtures/linear-sdk) and the relay's own.
[TestFixture]
public class LinearApiReadTests : ApiFixture
{
    private LinearApiWorld _world = null!;

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var database = await OpenDatabaseAsync();
        _world = await LinearApiWorld.CreateAsync(Api, database);
    }

    private async Task<JsonNode> OkAsync(string token, string document, object? variables = null)
    {
        var (status, body) = await LinearGraphQl.PostAsync(Client, token, LinearGraphQl.Document(document), variables);
        status.ShouldBe(HttpStatusCode.OK, body.ToJsonString());
        body["errors"].ShouldBeNull(body.ToJsonString());
        return body["data"]!;
    }

    private async Task<string?> ErrorCodeAsync(string token, string document, object? variables)
    {
        var (_, body) = await LinearGraphQl.PostAsync(Client, token, LinearGraphQl.Document(document), variables);
        return LinearGraphQl.ErrorCode(body);
    }

    [Test]
    public async Task Viewer_WhenTheSdkDocumentRuns_ShouldReturnTheConnectionAsAnAppUser()
    {
        var viewer = (await OkAsync(_world.TokenA, "viewer"))["viewer"]!;

        viewer["id"]!.GetValue<string>().ShouldBe(_world.ConnectionA.ToString());
        viewer["name"]!.GetValue<string>().ShouldBe(_world.NameA);
        viewer["app"]!.GetValue<bool>().ShouldBeTrue();
        viewer["createdAt"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Issue_WhenAskedByIdOrIdentifier_ShouldReturnTheMappedItem()
    {
        foreach (var id in new[] { _world.ItemA.ToString(), _world.KeyA })
        {
            var issue = (await OkAsync(_world.TokenA, "issue", new { id }))["issue"]!;

            issue["id"]!.GetValue<string>().ShouldBe(_world.ItemA.ToString());
            issue["identifier"]!.GetValue<string>().ShouldBe(_world.KeyA);
            issue["title"]!.GetValue<string>().ShouldBe("Add the thing");
            issue["description"]!.GetValue<string>().ShouldBe("Body\n\n## Acceptance criteria\n- [x] Works\n- [ ] Ships");
            issue["url"]!.GetValue<string>().ShouldEndWith($"/items/{_world.KeyA}");
            issue["branchName"]!.GetValue<string>().ShouldBe($"{_world.KeyA.ToLowerInvariant()}-add-the-thing");
            issue["team"]!["id"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
            issue["state"]!["id"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
            issue["creator"]!["id"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
            issue["labelIds"]!.AsArray().ShouldBeEmpty();
            issue["sharedAccess"]!["isShared"]!.GetValue<bool>().ShouldBeFalse();
        }
    }

    [Test]
    public async Task IssueComments_WhenPaged_ShouldFollowTheCursor()
    {
        var first = (await OkAsync(_world.TokenA, "issue_comments", new { id = _world.KeyA, first = 1 }))["issue"]!["comments"]!;

        first["nodes"]!.AsArray().Count.ShouldBe(1);
        first["nodes"]![0]!["body"]!.GetValue<string>().ShouldBe("First thoughts");
        first["nodes"]![0]!["id"]!.GetValue<string>().ShouldBe(_world.FirstCommentA.ToString());
        first["pageInfo"]!["hasNextPage"]!.GetValue<bool>().ShouldBeTrue();

        var rest = (await OkAsync(_world.TokenA, "issue_comments", new { id = _world.KeyA, first = 5, after = first["pageInfo"]!["endCursor"]!.GetValue<string>() }))["issue"]!["comments"]!;

        rest["nodes"]!.AsArray().Select(n => n!["body"]!.GetValue<string>()).ToArray().ShouldBe(["Second thoughts"]);
        rest["pageInfo"]!["hasNextPage"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Test]
    public async Task AgentSession_WhenTheSdkDocumentRuns_ShouldReturnStatusLinksPlanAndPeople()
    {
        var session = (await OkAsync(_world.TokenA, "agentSession", new { id = _world.SessionA }))["agentSession"]!;

        session["id"]!.GetValue<string>().ShouldBe(_world.SessionA.ToString());
        session["status"]!.GetValue<string>().ShouldBe("active");
        session["type"]!.GetValue<string>().ShouldBe("commentThread");
        session["appUser"]!["id"]!.GetValue<string>().ShouldBe(_world.ConnectionA.ToString());
        session["creator"]!["id"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
        session["externalLinks"]![0]!["label"]!.GetValue<string>().ShouldBe("Pull request");
        session["externalLinks"]![0]!["url"]!.GetValue<string>().ShouldEndWith("/pull/9");
        session["plan"]![0]!["content"]!.GetValue<string>().ShouldBe("Write it");
        session["endedAt"].ShouldBeNull();
    }

    [Test]
    public async Task AgentSessionActivities_WhenTheSdkDocumentRuns_ShouldReturnTypedContentInOrder()
    {
        var activities = (await OkAsync(_world.TokenA, "agentSession_activities", new { id = _world.SessionA }))["agentSession"]!["activities"]!["nodes"]!.AsArray();

        activities.Select(a => a!["content"]!["__typename"]!.GetValue<string>()).ToArray().ShouldBe(
            ["AgentActivityThoughtContent", "AgentActivityActionContent", "AgentActivityElicitationContent"]);
        activities[0]!["ephemeral"]!.GetValue<bool>().ShouldBeTrue();
        activities[1]!["content"]!["action"]!.GetValue<string>().ShouldBe("Run");
        activities[1]!["content"]!["parameter"]!.GetValue<string>().ShouldBe("dotnet test");
        activities[1]!["content"]!["result"]!.GetValue<string>().ShouldBe("passed");
        activities[2]!["signal"]!.GetValue<string>().ShouldBe("select");
    }

    [Test]
    public async Task AgentActivity_WhenTheSdkDocumentRuns_ShouldReturnTheRow()
    {
        var activity = (await OkAsync(_world.TokenA, "agentActivity", new { id = _world.FirstActivityA }))["agentActivity"]!;

        activity["content"]!["body"]!.GetValue<string>().ShouldBe("Reading the item");
        activity["agentSession"]!["id"]!.GetValue<string>().ShouldBe(_world.SessionA.ToString());
    }

    [Test]
    public async Task Reads_WhenTheyNameAnotherConnectionsItemOrSession_ShouldBeNotFound()
    {
        string?[] codes =
        [
            await ErrorCodeAsync(_world.TokenA, "issue", new { id = _world.ItemB }),
            await ErrorCodeAsync(_world.TokenA, "issue", new { id = _world.KeyB }),
            await ErrorCodeAsync(_world.TokenA, "issue_comments", new { id = _world.KeyB }),
            await ErrorCodeAsync(_world.TokenA, "agentSession", new { id = _world.SessionB }),
            await ErrorCodeAsync(_world.TokenA, "agentSession_activities", new { id = _world.SessionB }),
            await ErrorCodeAsync(_world.TokenB, "agentActivity", new { id = _world.FirstActivityA }),
            await ErrorCodeAsync(_world.TokenB, "issue", new { id = Guid.NewGuid() }),
        ];

        codes.ShouldAllBe(code => code == "NOT_FOUND");
    }

    [Test]
    public async Task IssueRepositorySuggestions_WhenTheRepoIsAmongTheCandidates_ShouldReturnItWithConfidenceOne()
    {
        var variables = new
        {
            issueId = _world.KeyA,
            agentSessionId = _world.SessionA,
            candidateRepositories = new[]
            {
                new { hostname = "github.com", repositoryFullName = "someone/else" },
                new { hostname = "GitHub.com", repositoryFullName = "shiplean/shiplean" },
            },
        };

        var suggestions = (await OkAsync(_world.TokenA, "issueRepositorySuggestions", variables))["issueRepositorySuggestions"]!["suggestions"]!.AsArray();

        var suggestion = suggestions.ShouldHaveSingleItem()!;
        suggestion["repositoryFullName"]!.GetValue<string>().ShouldBe("shiplean/shiplean");
        suggestion["hostname"]!.GetValue<string>().ShouldBe("GitHub.com");
        suggestion["confidence"]!.GetValue<double>().ShouldBe(1.0);
    }

    [Test]
    public async Task IssueRepositorySuggestions_WhenNoCandidateMatchesOrTheSessionIsOmitted_ShouldReturnNothingOrTheItemsRepo()
    {
        var none = new { issueId = _world.KeyA, candidateRepositories = new[] { new { hostname = "github.com", repositoryFullName = "someone/else" } } };
        var omitted = new { issueId = _world.KeyB, candidateRepositories = new[] { new { hostname = "github.com", repositoryFullName = "Other/Repo" } } };

        (await OkAsync(_world.TokenA, "issueRepositorySuggestions", none))["issueRepositorySuggestions"]!["suggestions"]!.AsArray().ShouldBeEmpty();
        (await OkAsync(_world.TokenB, "issueRepositorySuggestions", omitted))["issueRepositorySuggestions"]!["suggestions"]!.AsArray().Count.ShouldBe(1);
    }

    [Test]
    public async Task IssueRepositorySuggestions_WhenTheItemOrSessionIsAnotherConnections_ShouldBeNotFound()
    {
        var candidates = new[] { new { hostname = "github.com", repositoryFullName = "Other/Repo" } };

        string?[] codes =
        [
            await ErrorCodeAsync(_world.TokenA, "issueRepositorySuggestions", new { issueId = _world.KeyB, candidateRepositories = candidates }),
            await ErrorCodeAsync(_world.TokenA, "issueRepositorySuggestions", new { issueId = _world.KeyA, agentSessionId = _world.SessionB, candidateRepositories = candidates }),
        ];

        codes.ShouldAllBe(code => code == "NOT_FOUND");
    }

    [Test]
    public async Task RelayDocuments_WhenTheyAreRead_ShouldRunAgainstTheSchema()
    {
        var issue = (await OkAsync(_world.TokenA, "relay_issue", new { id = _world.KeyA }))["issue"]!;
        var suggestions = (await OkAsync(_world.TokenA, "relay_issueRepositorySuggestions", new
        {
            issueId = _world.KeyA,
            agentSessionId = _world.SessionA,
            candidateRepositories = new[] { new { hostname = "github.com", repositoryFullName = "ShipLean/ShipLean" } },
        }))["issueRepositorySuggestions"]!["suggestions"]!.AsArray();

        issue["identifier"]!.GetValue<string>().ShouldBe(_world.KeyA);
        suggestions.Count.ShouldBe(1);
    }

    [TestCase("createAgentActivity", "{\"input\":{\"agentSessionId\":\"00000000-0000-4000-8000-000000000001\",\"content\":{\"type\":\"thought\",\"body\":\"x\"}}}")]
    [TestCase("relay_agentActivityCreate", "{\"input\":{\"agentSessionId\":\"00000000-0000-4000-8000-000000000001\",\"content\":{\"type\":\"thought\",\"body\":\"x\"}}}")]
    [TestCase("updateAgentSession", "{\"id\":\"00000000-0000-4000-8000-000000000001\",\"input\":{\"plan\":[]}}")]
    [TestCase("relay_agentSessionUpdate", "{\"id\":\"00000000-0000-4000-8000-000000000001\",\"input\":{\"plan\":[]}}")]
    [TestCase("agentSessionUpdateExternalUrl", "{\"id\":\"00000000-0000-4000-8000-000000000001\",\"input\":{\"addedExternalUrls\":[{\"label\":\"PR\",\"url\":\"https://x.example/1\"}]}}")]
    [TestCase("createComment", "{\"input\":{\"issueId\":\"00000000-0000-4000-8000-000000000001\",\"body\":\"hi\"}}")]
    public async Task Mutations_WhenTheDocumentIsTheSdksOrTheRelays_ShouldValidateAndReachTheMutationResolver(string document, string variables)
    {
        var (_, body) = await LinearGraphQl.PostAsync(Client, _world.TokenA, LinearGraphQl.Document(document), JsonNode.Parse(variables));

        // C2 (SHIP-187) replaces the stubs; until then the document validates and the resolver says it is not there yet.
        LinearGraphQl.ErrorCode(body).ShouldBe("NOT_IMPLEMENTED", body.ToJsonString());
    }
}
