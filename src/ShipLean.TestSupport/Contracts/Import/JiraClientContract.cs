using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Import;

// Behaviour every IJiraClient must have, whatever it talks to. Each test runs against the Cloud and the Server flavour.
[TestFixture]
public abstract class JiraClientContract
{
    private const int SmallPage = 2;
    private const int PageLimit = 50;
    private static readonly string[] NoNumbers = [];
    private static readonly string[] BothPointFields = ["customfield_10016", "customfield_10028"];

    // A world for the flavour, broken as the fault says. The Server flavour pages the same issues as the Cloud one.
    protected abstract Task<JiraWorld> ArrangeAsync(JiraKind kind, JiraFault fault);

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Myself_ShouldNameTheSignedInUser(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var me = await world.Client.GetMyselfAsync(world.Credentials);

        me.DisplayName.ShouldNotBeNullOrWhiteSpace();
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Myself_WhenTokenWrong_ShouldBeValidation(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.WrongToken);

        var error = await Should.ThrowAsync<ApiException>(() => world.Client.GetMyselfAsync(world.Credentials));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Message.ShouldStartWith("Jira refused the email and token");
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_WhenTokenWrong_ShouldBeValidation(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.WrongToken);

        var error = await Should.ThrowAsync<ApiException>(() => world.Client.SearchIssuesAsync(world.Credentials, world.ProjectKey, null, SmallPage, NoNumbers));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Client_WhenJiraDown_ShouldBeNotConfigured(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.Down);

        var onMyself = await Should.ThrowAsync<ApiException>(() => world.Client.GetMyselfAsync(world.Credentials));
        var onSearch = await Should.ThrowAsync<ApiException>(() => world.Client.SearchIssuesAsync(world.Credentials, world.ProjectKey, null, SmallPage, NoNumbers));

        onMyself.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        onSearch.Code.ShouldBe(ApiErrorCodes.NotConfigured);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task ListProjects_ShouldCountTheIssuesOfTheSampleProject(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var projects = await world.Client.ListProjectsAsync(world.Credentials);

        var project = projects.Where(p => p.Key == world.ProjectKey).ShouldHaveSingleItem();
        project.Name.ShouldNotBeNullOrWhiteSpace();
        project.IssueCount.ShouldBe(world.IssueKeys.Count);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task GetIssueTypes_ShouldSeparateSubtasksFromTheRest(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var types = await world.Client.GetIssueTypesAsync(world.Credentials, world.ProjectKey);

        types.Any(t => t.Subtask).ShouldBeTrue();
        types.Any(t => !t.Subtask && t.Name == "Story").ShouldBeTrue();
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task GetStatuses_ShouldCarryJirasCategories(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var statuses = await world.Client.GetStatusesAsync(world.Credentials, world.ProjectKey);

        statuses.Select(s => s.Category).Distinct().Order().ShouldBe([JiraStatusCategory.New, JiraStatusCategory.Indeterminate, JiraStatusCategory.Done]);
        statuses.ShouldAllBe(s => s.IssueTypes.Count > 0);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task GetIssueTypes_WhenProjectUnknown_ShouldBeNotFound(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var error = await Should.ThrowAsync<ApiException>(() => world.Client.GetIssueTypesAsync(world.Credentials, "NOPE"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task GetFields_ShouldIncludeACustomStoryPointsField(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var fields = await world.Client.GetFieldsAsync(world.Credentials);

        fields.ShouldContain(f => f.Custom && f.Name.Contains("point", StringComparison.OrdinalIgnoreCase));
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_WhenPaged_ShouldReturnEveryIssueOnce(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var keys = (await ReadAllAsync(world, SmallPage)).Select(i => i.Key).ToList();

        keys.Order().ShouldBe(world.IssueKeys.Order());
        keys.Distinct().Count().ShouldBe(keys.Count);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_WhenPageCoversEverything_ShouldHaveNoContinuation(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var page = await world.Client.SearchIssuesAsync(world.Credentials, world.ProjectKey, null, 100, NoNumbers);

        page.Continuation.ShouldBeNull();
        page.Issues.Count.ShouldBe(world.IssueKeys.Count);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_WhenPageIsSmall_ShouldOfferAContinuation(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var page = await world.Client.SearchIssuesAsync(world.Credentials, world.ProjectKey, null, SmallPage, NoNumbers);

        page.Issues.Count.ShouldBe(SmallPage);
        page.Continuation.ShouldNotBeNull();
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_ShouldCarryTheChangelogOldestFirst(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var issues = await ReadAllAsync(world, SmallPage);

        issues.Any(i => i.Changelog.Count >= 2).ShouldBeTrue();
        issues.ShouldAllBe(i => i.Changelog.Select(c => c.Created).SequenceEqual(i.Changelog.Select(c => c.Created).Order()));
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_ShouldCarrySprintsWithDates(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var sprints = (await ReadAllAsync(world, SmallPage)).SelectMany(i => i.Sprints).ToList();

        // Sprint 11 is closed and ran from 31 Aug 01:00 to 28 Sep 10:00 UTC; a future sprint has no dates.
        var closed = sprints.First(s => s.Id == 111);
        closed.Name.ShouldBe("Platform Sprint 11");
        closed.State.ShouldBe("closed");
        closed.StartDate.ShouldNotBeNull().UtcDateTime.ShouldBe(new DateTime(2026, 8, 31, 1, 0, 0, DateTimeKind.Utc));
        closed.EndDate.ShouldNotBeNull().UtcDateTime.ShouldBe(new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc));
        closed.Goal.ShouldNotBeNullOrWhiteSpace();
        sprints.ShouldAllBe(s => s.State == "active" || s.State == "closed" || s.State == "future");
        sprints.Where(s => s.State == "future").ShouldAllBe(s => s.StartDate == null && s.EndDate == null);
        sprints.Where(s => s.State != "future").ShouldAllBe(s => s.StartDate != null && s.EndDate != null && s.StartDate < s.EndDate);
        sprints.Any(s => s.State == "future").ShouldBeTrue();
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_ShouldCarryTheEpicAsParent(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var issues = await ReadAllAsync(world, SmallPage);

        issues.Single(i => i.Key == world.EpicChildKey).ParentKey.ShouldBe(world.EpicKey);
        issues.Single(i => i.Key == world.EpicKey).ParentKey.ShouldBeNull();
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_ShouldMarkAutomationUsersAsBots(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var issues = await ReadAllAsync(world, SmallPage);

        var bots = issues.SelectMany(i => i.Changelog).Select(c => c.Author).OfType<JiraUser>().Where(u => u.IsBot).Select(u => u.AccountId).Distinct().ToList();
        bots.ShouldBe(world.BotAccountId is null ? [] : [world.BotAccountId]);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_WhenNumberFieldsRequested_ShouldReturnTheirValues(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var issues = await ReadAllAsync(world, SmallPage, BothPointFields);

        var story = issues.Single(i => i.Key == world.CommentedIssueKey);
        story.Numbers.ShouldBe(new Dictionary<string, decimal> { ["customfield_10016"] = 5m, ["customfield_10028"] = 6.5m });
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_WhenOneNumberFieldRequested_ShouldReturnOnlyThatField(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var issues = await ReadAllAsync(world, SmallPage, ["customfield_10028"]);

        issues.SelectMany(i => i.Numbers.Keys).Distinct().ShouldBe(["customfield_10028"]);
        issues.Single(i => i.Key == world.CommentedIssueKey).Numbers["customfield_10028"].ShouldBe(6.5m);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_WhenNoNumberFieldsRequested_ShouldReturnNoNumbers(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var issues = await ReadAllAsync(world, SmallPage);

        issues.ShouldAllBe(i => i.Numbers.Count == 0);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_ShouldTurnDescriptionsIntoPlainText(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var issues = await ReadAllAsync(world, SmallPage);

        issues.Select(i => i.Description).ShouldAllBe(d => d != null && d.Length > 0 && !d.Contains('{') && !d.Contains("\"type\""));
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task Search_WhenProjectUnknown_ShouldBeNotFound(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var error = await Should.ThrowAsync<ApiException>(() => world.Client.SearchIssuesAsync(world.Credentials, "NOPE", null, SmallPage, NoNumbers));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task GetComments_ShouldReturnThemOldestFirst(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var comments = await world.Client.GetCommentsAsync(world.Credentials, world.CommentedIssueKey);

        comments.Count.ShouldBe(world.CommentCount);
        comments.Select(c => c.Created).ShouldBe(comments.Select(c => c.Created).Order());
        comments.ShouldAllBe(c => c.Body.Length > 0 && !c.Body.Contains("\"type\""));
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task GetComments_WhenIssueUnknown_ShouldBeNotFound(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);

        var error = await Should.ThrowAsync<ApiException>(() => world.Client.GetCommentsAsync(world.Credentials, world.ProjectKey + "-99999"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task OpenAttachment_ShouldReturnTheBytes(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);
        var attachment = (await ReadAllAsync(world, SmallPage)).SelectMany(i => i.Attachments).First();

        await using var stream = await world.Client.OpenAttachmentAsync(world.Credentials, attachment);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        copy.ToArray().ShouldBe(world.AttachmentBytes);
        attachment.Size.ShouldBe(world.AttachmentBytes.Length);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task OpenAttachment_WhenRedirectedOffTheSite_ShouldNotSendTheToken(JiraKind kind)
    {
        // The world's media host answers 401 to any request that carries Authorization, so only a client that drops the token on the redirect gets the bytes.
        var world = await ArrangeAsync(kind, JiraFault.RedirectOffSite);
        var attachment = (await ReadAllAsync(world, SmallPage)).SelectMany(i => i.Attachments).First();

        await using var stream = await world.Client.OpenAttachmentAsync(world.Credentials, attachment);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        copy.ToArray().ShouldBe(world.AttachmentBytes);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task OpenAttachment_WhenUrlIsOffTheSite_ShouldBeValidation(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);
        var attachment = new JiraAttachment("1", "x.png", "image/png", 1, "https://elsewhere.example/secure/attachment/1/x.png");

        var error = await Should.ThrowAsync<ApiException>(() => world.Client.OpenAttachmentAsync(world.Credentials, attachment));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task OpenAttachment_WhenUrlHasAnotherPort_ShouldBeValidation(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.None);
        var onTheHostButOtherPort = new UriBuilder(world.Credentials.BaseUrl) { Port = 8443, Path = "/secure/attachment/1/x.png" }.Uri.ToString();
        var attachment = new JiraAttachment("1", "x.png", "image/png", 1, onTheHostButOtherPort);

        var error = await Should.ThrowAsync<ApiException>(() => world.Client.OpenAttachmentAsync(world.Credentials, attachment));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase(JiraKind.Cloud)]
    [TestCase(JiraKind.Server)]
    public async Task OpenAttachment_WhenTokenWrong_ShouldBeValidation(JiraKind kind)
    {
        var world = await ArrangeAsync(kind, JiraFault.WrongToken);
        var attachment = new JiraAttachment("1", "any.png", "image/png", 1, world.Credentials.BaseUrl + "/secure/attachment/1/any.png");

        var error = await Should.ThrowAsync<ApiException>(() => world.Client.OpenAttachmentAsync(world.Credentials, attachment));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    private static async Task<List<JiraIssue>> ReadAllAsync(JiraWorld world, int pageSize, IReadOnlyList<string>? numberFieldIds = null)
    {
        var issues = new List<JiraIssue>();
        string? continuation = null;
        for (var pages = 0; pages < PageLimit; pages++)
        {
            var page = await world.Client.SearchIssuesAsync(world.Credentials, world.ProjectKey, continuation, pageSize, numberFieldIds ?? NoNumbers);
            issues.AddRange(page.Issues);
            continuation = page.Continuation;
            if (continuation is null)
            {
                return issues;
            }
        }
        throw new InvalidOperationException("The search never reached its last page.");
    }
}
