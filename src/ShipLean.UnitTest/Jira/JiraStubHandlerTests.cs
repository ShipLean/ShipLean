using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Import;
using ShipLean.TestSupport.Jira;

namespace ShipLean.UnitTest.Jira;

[TestFixture]
public class JiraStubHandlerTests
{
    private const string CloudSearch = "/rest/api/3/search/jql?jql=project%20%3D%20PLATFORM&maxResults=5&fields=*all&expand=changelog";
    private const string ServerSearch = "/rest/api/2/search?jql=project%20%3D%20PLATFORM&maxResults=5&expand=changelog";

    private static HttpClient Client(JiraStubHandler handler, JiraKind kind, string? token = null)
    {
        var credentials = JiraStubHandler.Credentials(kind, token);
        var client = new HttpClient(handler) { BaseAddress = new Uri(credentials.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = kind == JiraKind.Cloud
            ? new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Email}:{credentials.Token}")))
            : new AuthenticationHeaderValue("Bearer", credentials.Token);
        return client;
    }

    private static async Task<JsonObject> GetAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    }

    [Test]
    public async Task Stub_WhenCloudSearchPaged_ShouldServeTwoPages()
    {
        var handler = new JiraStubHandler();
        using var client = Client(handler, JiraKind.Cloud);

        var first = await GetAsync(client, CloudSearch);
        var second = await GetAsync(client, CloudSearch + "&nextPageToken=" + Uri.EscapeDataString((string)first["nextPageToken"]!));

        first["issues"]!.AsArray().Count.ShouldBe(5);
        second["issues"]!.AsArray().Count.ShouldBe(5);
        second["nextPageToken"].ShouldBeNull();
        ((bool)first["isLast"]!).ShouldBeFalse();
        ((bool)second["isLast"]!).ShouldBeTrue();
        first["issues"]!.AsArray().Concat(second["issues"]!.AsArray()).Select(i => (string)i!["key"]!).ShouldBe(JiraStubFiles.IssueKeys);
        handler.SearchRequestCount.ShouldBe(2);
    }

    [Test]
    public async Task Stub_WhenServerSearchPaged_ShouldServeTwoPages()
    {
        var handler = new JiraStubHandler();
        using var client = Client(handler, JiraKind.Server);

        var first = await GetAsync(client, ServerSearch);
        var second = await GetAsync(client, ServerSearch + "&startAt=5");

        ((int)first["total"]!).ShouldBe(10);
        first["issues"]!.AsArray().Concat(second["issues"]!.AsArray()).Select(i => (string)i!["key"]!).ShouldBe(JiraStubFiles.IssueKeys);
        handler.SearchRequestCount.ShouldBe(2);
    }

    [Test]
    public async Task Stub_WhenCloudSearchPostedAsJson_ShouldPageLikeTheGet()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);
        var body = new StringContent("""{"jql":"project = PLATFORM","maxResults":3,"fields":["*all"]}""", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/rest/api/3/search/jql", body);

        var page = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        page["issues"]!.AsArray().Count.ShouldBe(3);
        page["nextPageToken"].ShouldNotBeNull();
    }

    [Test]
    public async Task Stub_WhenCloudSearchHasNoFields_ShouldReturnOnlyIds()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var page = await GetAsync(client, "/rest/api/3/search/jql?jql=project%20%3D%20PLATFORM&maxResults=2");

        page["issues"]!.AsArray().ShouldAllBe(i => i!.AsObject().Count == 1 && i["id"] != null);
    }

    [TestCase(JiraKind.Cloud, "/rest/api/3/issue/PLATFORM-404")]
    [TestCase(JiraKind.Server, "/rest/api/2/issue/PLATFORM-404")]
    public async Task Stub_WhenChangelogNotExpanded_ShouldOmitIt(JiraKind kind, string path)
    {
        using var client = Client(new JiraStubHandler(), kind);

        var plain = await GetAsync(client, path);
        var expanded = await GetAsync(client, path + "?expand=changelog");

        plain["changelog"].ShouldBeNull();
        expanded["changelog"]!["histories"]!.AsArray().Count.ShouldBe(4);
    }

    [Test]
    public async Task Stub_WhenServer_ShouldSendPlainTextAndUsersByName()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Server);

        var issue = await GetAsync(client, "/rest/api/2/issue/PLATFORM-412");
        var comments = await GetAsync(client, "/rest/api/2/issue/PLATFORM-412/comment");

        ((string)issue["fields"]!["description"]!).ShouldContain("Dana owns the cut-over plan.");
        issue["fields"]!["assignee"]!["accountId"].ShouldBeNull();
        ((string)issue["fields"]!["assignee"]!["name"]!).ShouldBe("dana.whitfield");
        ((string)comments["comments"]![0]!["body"]!).ShouldStartWith("Cut-over plan drafted");
    }

    [Test]
    public async Task Stub_WhenCloud_ShouldSendAtlassianDocumentFormat()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var issue = await GetAsync(client, "/rest/api/3/issue/PLATFORM-412");

        ((string)issue["fields"]!["description"]!["type"]!).ShouldBe("doc");
        ((string)issue["fields"]!["assignee"]!["accountId"]!).ShouldNotBeNullOrEmpty();
    }

    [TestCase(JiraKind.Cloud, "/rest/api/3/myself")]
    [TestCase(JiraKind.Server, "/rest/api/2/myself")]
    [TestCase(JiraKind.Cloud, "/rest/api/3/project/search")]
    [TestCase(JiraKind.Server, "/rest/api/2/project")]
    [TestCase(JiraKind.Cloud, "/rest/api/3/project/PLATFORM")]
    [TestCase(JiraKind.Server, "/rest/api/2/project/PLATFORM/statuses")]
    [TestCase(JiraKind.Cloud, "/rest/api/3/field")]
    [TestCase(JiraKind.Server, "/rest/api/2/issue/PLATFORM-404/comment")]
    [TestCase(JiraKind.Cloud, "/rest/api/3/issue/PLATFORM-410/comment")]
    public async Task Stub_WhenRouteIsAsked_ShouldServeIt(JiraKind kind, string path)
    {
        using var client = Client(new JiraStubHandler(), kind);

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Stub_WhenCountAsked_ShouldCountTheProjectsIssues()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var response = await client.PostAsync("/rest/api/3/search/approximate-count", new StringContent("""{"jql":"project = PLATFORM"}""", Encoding.UTF8, "application/json"));

        ((int)JsonNode.Parse(await response.Content.ReadAsStringAsync())!["count"]!).ShouldBe(10);
    }

    [TestCase(JiraKind.Cloud, "/rest/api/3/issue/PLATFORM-999")]
    [TestCase(JiraKind.Server, "/rest/api/2/project/NOPE")]
    [TestCase(JiraKind.Server, "/rest/api/3/myself")]
    public async Task Stub_WhenThingIsMissing_ShouldAnswer404(JiraKind kind, string path)
    {
        using var client = Client(new JiraStubHandler(), kind);

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Stub_WhenServerAttachmentFetched_ShouldServeTheBytes()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Server);
        var issue = await GetAsync(client, $"/rest/api/2/issue/{JiraStubFiles.AttachmentIssueKey}");
        var content = (string)issue["fields"]!["attachment"]![0]!["content"]!;

        var bytes = await client.GetByteArrayAsync(content);

        content.ShouldBe($"{JiraStubHandler.ServerUrl}/secure/attachment/10601/load-test-dashboard.png");
        bytes.ShouldBe(JiraStubFiles.AttachmentBytes);
    }

    [Test]
    public async Task Stub_WhenCloudAttachmentContentFetched_ShouldAnswer303ToAnotherHost()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);
        var issue = await GetAsync(client, $"/rest/api/3/issue/{JiraStubFiles.AttachmentIssueKey}");
        var content = (string)issue["fields"]!["attachment"]![0]!["content"]!;

        var redirect = await client.GetAsync(content);
        using var anonymous = new HttpClient(new JiraStubHandler());
        var media = await anonymous.GetAsync(redirect.Headers.Location);

        content.ShouldBe($"{JiraStubHandler.CloudUrl}/rest/api/3/attachment/content/10601");
        redirect.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        redirect.Headers.Location!.Host.ShouldBe(JiraStubHandler.MediaHost);
        redirect.Headers.Location.Scheme.ShouldBe("https");
        media.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await media.Content.ReadAsByteArrayAsync()).ShouldBe(JiraStubFiles.AttachmentBytes);
    }

    [Test]
    public async Task Stub_WhenMediaRequestCarriesAuthorization_ShouldAnswer401()
    {
        using var client = new HttpClient(new JiraStubHandler());
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{JiraStubHandler.MediaHost}/media/10601/load-test-dashboard.png?token={JiraStubHandler.MediaToken}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JiraStubHandler.ServerToken);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Stub_WhenMediaUrlHasNoToken_ShouldAnswer401()
    {
        using var client = new HttpClient(new JiraStubHandler());

        var response = await client.GetAsync($"https://{JiraStubHandler.MediaHost}/media/10601/load-test-dashboard.png");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Stub_WhenServerRedirectsOffSite_ShouldAnswer302ToTheMediaHost()
    {
        using var client = Client(new JiraStubHandler { ServerRedirectsAttachmentsOffSite = true }, JiraKind.Server);

        var redirect = await client.GetAsync($"{JiraStubHandler.ServerUrl}/secure/attachment/10601/load-test-dashboard.png");

        redirect.StatusCode.ShouldBe(HttpStatusCode.Found);
        redirect.Headers.Location!.Host.ShouldBe(JiraStubHandler.MediaHost);
    }

    [Test]
    public async Task Stub_WhenCloud_ShouldSendOneAutomationUserWithAccountTypeApp()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var issue = await GetAsync(client, "/rest/api/3/issue/PLATFORM-404?expand=changelog");

        var authors = issue["changelog"]!["histories"]!.AsArray().Select(h => h!["author"]!).Where(a => (string?)a["accountType"] == "app").ToList();
        ((string)authors.ShouldHaveSingleItem()["displayName"]!).ShouldBe("Automation for Jira");
    }

    [Test]
    public async Task Stub_WhenServer_ShouldNotSendAnAccountType()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Server);

        var issue = await GetAsync(client, "/rest/api/2/issue/PLATFORM-404?expand=changelog");

        issue["changelog"]!["histories"]!.AsArray().ShouldAllBe(h => h!["author"]!["accountType"] == null);
    }

    [Test]
    public async Task Stub_WhenCloud_ShouldSendSprintsWithDatesAndTheFutureOneWithout()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var closed = (await GetAsync(client, "/rest/api/3/issue/PLATFORM-404"))["fields"]!["customfield_10020"]!.AsArray();
        var future = (await GetAsync(client, "/rest/api/3/issue/PLATFORM-410"))["fields"]!["customfield_10020"]!.AsArray();

        ((string)closed[1]!["startDate"]!).ShouldBe("2026-08-31T01:00:00.000Z");
        ((string)closed[1]!["goal"]!).ShouldNotBeNullOrEmpty();
        ((string)future[0]!["state"]!).ShouldBe("future");
        future[0]!["startDate"].ShouldBeNull();
    }

    [Test]
    public async Task Stub_WhenServer_ShouldSendSprintsAsGreenhopperStrings()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Server);

        var closed = (await GetAsync(client, "/rest/api/2/issue/PLATFORM-404"))["fields"]!["customfield_10020"]!.AsArray();
        var future = (await GetAsync(client, "/rest/api/2/issue/PLATFORM-410"))["fields"]!["customfield_10020"]!.AsArray();

        var text = (string)closed[1]!;
        text.ShouldStartWith("com.atlassian.greenhopper.service.sprint.Sprint@");
        text.ShouldContain("[id=111,rapidViewId=34,state=CLOSED,name=Platform Sprint 11,startDate=2026-08-31T01:00:00.000Z,endDate=2026-09-28T10:00:00.000Z,");
        ((string)future[0]!).ShouldContain("state=FUTURE,name=Platform Sprint 13,startDate=<null>,endDate=<null>");
    }

    [Test]
    public async Task Stub_WhenServer_ShouldPutAStoryUnderItsEpicThroughTheEpicLinkField()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Server);

        var story = (await GetAsync(client, "/rest/api/2/issue/PLATFORM-404"))["fields"]!;
        var subtask = (await GetAsync(client, "/rest/api/2/issue/PLATFORM-408"))["fields"]!;
        var fields = (await client.GetFromJsonAsync<JsonArray>("/rest/api/2/field"))!;

        ((string)story["customfield_10014"]!).ShouldBe("PLATFORM-403");
        story["parent"].ShouldBeNull();
        ((string)subtask["parent"]!["key"]!).ShouldBe("PLATFORM-407");
        fields.Select(f => (string)f!["id"]!).ShouldContain("customfield_10014");
    }

    [Test]
    public async Task Stub_WhenCloud_ShouldKeepTheEpicInParentAndNotOfferEpicLink()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var story = (await GetAsync(client, "/rest/api/3/issue/PLATFORM-404"))["fields"]!;
        var fields = (await client.GetFromJsonAsync<JsonArray>("/rest/api/3/field"))!;

        ((string)story["parent"]!["key"]!).ShouldBe("PLATFORM-403");
        fields.Select(f => (string)f!["id"]!).ShouldNotContain("customfield_10014");
    }

    [Test]
    public async Task Stub_WhenFieldsAsked_ShouldTypeTheNumberFields()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var fields = (await client.GetFromJsonAsync<JsonArray>("/rest/api/3/field"))!.ToDictionary(f => (string)f!["id"]!);

        ((string)fields["customfield_10016"]!["schema"]!["type"]!).ShouldBe("number");
        ((string)fields["customfield_10016"]!["schema"]!["custom"]!).ShouldEndWith(":jsw-story-points");
        ((string)fields["customfield_10028"]!["schema"]!["custom"]!).ShouldEndWith(":float");
    }

    [Test]
    public async Task Stub_WhenStoryPointsAreInTwoFields_ShouldServeBoth()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var fields = (await GetAsync(client, "/rest/api/3/issue/PLATFORM-404"))["fields"]!;

        ((decimal)fields["customfield_10016"]!).ShouldBe(5m);
        ((decimal)fields["customfield_10028"]!).ShouldBe(6.5m);
    }

    [Test]
    public async Task Stub_WhenServerAsksWhoAmI_ShouldLinkTheUserByUsername()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Server);

        var me = await GetAsync(client, "/rest/api/2/myself");
        var assignee = (await GetAsync(client, "/rest/api/2/issue/PLATFORM-405"))["fields"]!["assignee"]!;

        ((string)me["self"]!).ShouldEndWith("/user?username=priya.shah");
        ((string)assignee["self"]!).ShouldEndWith("/user?username=mei.lin");
    }

    [Test]
    public async Task Stub_WhenCloudUserHidesTheirEmail_ShouldOmitIt()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);

        var dana = (await GetAsync(client, "/rest/api/3/issue/PLATFORM-412"))["fields"]!["assignee"]!;
        var mei = (await GetAsync(client, "/rest/api/3/issue/PLATFORM-405"))["fields"]!["assignee"]!;

        dana["emailAddress"].ShouldBeNull();
        ((string)mei["emailAddress"]!).ShouldBe("mei.lin@acme.io");
    }

    [TestCase(JiraKind.Cloud, "/rest/api/3/search/jql")]
    [TestCase(JiraKind.Server, "/rest/api/2/search")]
    public async Task Stub_WhenUnknownProjectSearched_ShouldAnswer400WithJirasText(JiraKind kind, string path)
    {
        using var client = Client(new JiraStubHandler(), kind);

        var response = await client.GetAsync(path + "?jql=project%20%3D%20NOPE&maxResults=5");
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ((string)body["errorMessages"]![0]!).ShouldBe("The value 'NOPE' does not exist for the field 'project'.");
    }

    [Test]
    public void Stub_WhenChangelogIsStoredRaw_ShouldHoldPlatformFourOhFiveNewestFirst()
    {
        var histories = JsonNode.Parse(JiraStubFiles.ReadText("issues", "PLATFORM-405.json"))!["changelog"]!["histories"]!.AsArray();

        // Every timestamp is in +0000, so the text sorts like time.
        var created = histories.Select(h => (string)h!["created"]!).ToList();
        created.ShouldBe(created.OrderByDescending(c => c, StringComparer.Ordinal));
        created.Count.ShouldBeGreaterThan(1);
    }

    [TestCase(JiraKind.Cloud, "/rest/api/3/myself")]
    [TestCase(JiraKind.Server, "/rest/api/2/myself")]
    [TestCase(JiraKind.Cloud, "/secure/attachment/10601/load-test-dashboard.png")]
    public async Task Stub_WhenWrongToken_ShouldAnswer401(JiraKind kind, string path)
    {
        using var client = Client(new JiraStubHandler(), kind, "not-the-token");

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Stub_WhenNoCredentialsSent_ShouldAnswer401(JiraKind kind)
    {
        using var client = new HttpClient(new JiraStubHandler()) { BaseAddress = new Uri(JiraStubHandler.Credentials(kind).BaseUrl) };

        var response = await client.GetAsync(kind == JiraKind.Cloud ? "/rest/api/3/myself" : "/rest/api/2/myself");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(JiraKind.Cloud, "/rest/api/3/myself")]
    [TestCase(JiraKind.Server, "/rest/api/2/search?jql=project%20%3D%20PLATFORM")]
    public async Task Stub_WhenJiraIsDown_ShouldAnswer503(JiraKind kind, string path)
    {
        var handler = new JiraStubHandler { IsDown = true };
        using var client = Client(handler, kind);

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        handler.RequestCount.ShouldBe(1);
    }

    [Test]
    public async Task Stub_WhenHostIsUnknown_ShouldFailLikeAnUnreachableSite()
    {
        using var client = new HttpClient(new JiraStubHandler());

        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://nowhere.example/rest/api/3/myself"));
    }

    [Test]
    public async Task Stub_ShouldHoldTheSampleProject()
    {
        using var client = Client(new JiraStubHandler(), JiraKind.Cloud);
        var issues = (await GetAsync(client, CloudSearch.Replace("maxResults=5", "maxResults=50", StringComparison.Ordinal)))["issues"]!.AsArray().Select(i => i!["fields"]!).ToList();

        issues.Count.ShouldBe(10);
        issues.Count(f => (string)f["issuetype"]!["name"]! == "Epic").ShouldBe(1);
        issues.Count(f => f["parent"] is { } parent && (string)parent["key"]! == "PLATFORM-403").ShouldBe(2);
        issues.Count(f => f["parent"] is { } parent && (string)parent["key"]! == "PLATFORM-407").ShouldBe(2);
        issues.Count(f => (string)f["issuetype"]!["name"]! == "Bug").ShouldBe(1);
        issues.Count(f => (string)f["issuetype"]!["name"]! == "Story" && f["customfield_10016"] == null && f["parent"] == null).ShouldBeGreaterThan(0);
        issues.Any(f => (string)f["status"]!["name"]! == "Waiting for Vendor").ShouldBeTrue();
        issues.Any(f => (string?)f["assignee"]?["displayName"] == "Dana Whitfield").ShouldBeTrue();
    }

    [Test]
    public void Stub_EveryRawFile_ShouldBeValidJson()
    {
        var files = Directory.EnumerateFiles(JiraStubFiles.Directory, "*.json", SearchOption.AllDirectories).ToList();

        files.Count.ShouldBeGreaterThanOrEqualTo(20);
        foreach (var file in files)
        {
            Should.NotThrow(() => JsonNode.Parse(File.ReadAllText(file)), file);
        }
    }
}
