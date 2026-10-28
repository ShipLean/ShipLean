using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Comments;

// Behaviour every ICommentStore must have, whatever it stores data in. Users are the seeded Joel and Priya; a store made
// for an actor acts as that user, and each store is used straight after it is made.
[TestFixture]
public abstract class CommentStoreContract
{
    private static readonly Guid Joel = SeedIds.Users.Joel;
    private static readonly Guid Priya = SeedIds.Users.Priya;

    protected abstract ICommentStore CreateStore(Guid actorId);

    // A store for the same actor with an agent acting for them. Verified with a host stamps "Name (host)"; unverified
// stamps "Name (unverified)". CreateStore(actorId) means no agent is acting.
    protected abstract ICommentStore CreateStore(Guid actorId, string agentName, string? agentHost, bool agentVerified);

    protected abstract Task<Guid> ArrangeWorkItemAsync();

    [Test]
    public async Task Add_WhenListed_ShouldReturnTheCommentOldestFirst()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);

        var first = await store.AddAsync(workItem, new CommentCreate("First, with **bold**"));
        var second = await store.AddAsync(workItem, new CommentCreate("Second"));

        var listed = await store.ListAsync(workItem);
        listed.Select(c => c.Id).ShouldBe([first.Id, second.Id]);
        first.Author.ShouldNotBeNull().Id.ShouldBe(Joel);
        first.WorkItemId.ShouldBe(workItem);
        first.Body.ShouldBe("First, with **bold**");
        first.BodyHtml.ShouldContain("<strong>bold</strong>");
        first.EditedAt.ShouldBeNull();
        first.CreatedLabel.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Add_WhenAgentActive_ShouldStampAgentName()
    {
        var workItem = await ArrangeWorkItemAsync();

        var verified = await CreateStore(Joel, "Claude Code", "claude.ai", true).AddAsync(workItem, new CommentCreate("From the agent"));
        var unverified = await CreateStore(Joel, "Claude Code", null, false).AddAsync(workItem, new CommentCreate("From an unverified agent"));
        var person = await CreateStore(Joel).AddAsync(workItem, new CommentCreate("From a person"));

        verified.AgentName.ShouldBe("Claude Code (claude.ai)");
        unverified.AgentName.ShouldBe("Claude Code (unverified)");
        person.AgentName.ShouldBeNull();
        var listed = await CreateStore(Joel).ListAsync(workItem);
        listed.Select(c => c.AgentName).ShouldBe(["Claude Code (claude.ai)", "Claude Code (unverified)", null]);
    }

    [Test]
    public async Task AddAsAgent_WhenGivenAName_ShouldStampItAndKeepTheCaller()
    {
        var workItem = await ArrangeWorkItemAsync();

        var added = await CreateStore(Joel).AddAsAgentAsync(workItem, new CommentCreate("Done: **merged**"), "Relay", Priya);

        added.AgentName.ShouldBe("Relay");
        added.Author.ShouldNotBeNull().Id.ShouldBe(Priya);
        added.BodyHtml.ShouldContain("<strong>merged</strong>");
        var listed = await CreateStore(Joel).ListAsync(workItem);
        listed.Select(c => c.AgentName).ShouldBe(["Relay"]);
    }

    [Test]
    public async Task Add_WhenBodyIsBlank_ShouldBeValidation()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);

        var error = await Should.ThrowAsync<ApiException>(() => store.AddAsync(workItem, new CommentCreate("  ")));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Add_WhenWorkItemIsUnknown_ShouldBeNotFound()
    {
        var store = CreateStore(Joel);

        var error = await Should.ThrowAsync<ApiException>(() => store.AddAsync(Guid.NewGuid(), new CommentCreate("Hello")));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Edit_WhenAuthor_ShouldChangeTheBodyAndMarkItEdited()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var comment = await store.AddAsync(workItem, new CommentCreate("Before"));

        var edited = await store.EditAsync(comment.Id, new CommentEdit("After"));

        edited.Id.ShouldBe(comment.Id);
        edited.Body.ShouldBe("After");
        edited.EditedAt.ShouldNotBeNull();
        edited.EditedLabel.ShouldNotBeNullOrWhiteSpace();
        edited.CreatedAt.ShouldBe(comment.CreatedAt);
    }

    [Test]
    public async Task Edit_WhenNotAuthor_ShouldBeForbidden()
    {
        var workItem = await ArrangeWorkItemAsync();
        var comment = await CreateStore(Joel).AddAsync(workItem, new CommentCreate("Mine"));
        var other = CreateStore(Priya);

        var error = await Should.ThrowAsync<ApiException>(() => other.EditAsync(comment.Id, new CommentEdit("Hijacked")));

        error.Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await other.ListAsync(workItem)).Single().Body.ShouldBe("Mine");
    }

    [Test]
    public async Task Archive_WhenListed_ShouldBeHidden()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var kept = await store.AddAsync(workItem, new CommentCreate("Kept"));
        var gone = await store.AddAsync(workItem, new CommentCreate("Gone"));

        await store.ArchiveAsync(gone.Id);

        (await store.ListAsync(workItem)).Select(c => c.Id).ShouldBe([kept.Id]);
        var error = await Should.ThrowAsync<ApiException>(() => store.EditAsync(gone.Id, new CommentEdit("Again")));
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Supersede_WhenMarked_ShouldStampWhoAndKeepItListed()
    {
        var workItem = await ArrangeWorkItemAsync();
        var comment = await CreateStore(Joel).AddAsync(workItem, new CommentCreate("Old plan"));

        var marked = await CreateStore(Priya).SupersedeAsync(comment.Id);
        var again = await CreateStore(Joel).SupersedeAsync(comment.Id);

        comment.Superseded.ShouldBeNull();
        marked.Superseded.ShouldNotBeNull().By.Id.ShouldBe(Priya);
        again.Superseded.ShouldNotBeNull().By.Id.ShouldBe(Priya);
        again.Superseded.At.ShouldBe(marked.Superseded.At);
        (await CreateStore(Joel).ListAsync(workItem)).Single().Superseded.ShouldNotBeNull().By.Id.ShouldBe(Priya);
    }

    [Test]
    public async Task Supersede_WhenArchived_ShouldBeNotFound()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var comment = await store.AddAsync(workItem, new CommentCreate("Gone"));
        await store.ArchiveAsync(comment.Id);

        var error = await Should.ThrowAsync<ApiException>(() => store.SupersedeAsync(comment.Id));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Import_WhenRerun_ShouldUpdateNotDuplicate()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var created = new DateTimeOffset(2025, 3, 2, 9, 30, 0, TimeSpan.Zero);
        var first = await store.ImportAsync(workItem, Imported("10001", "Original", created, Priya));
        (await store.ListAsync(workItem)).ShouldHaveSingleItem().EditedAt.ShouldBeNull();

        var second = await store.ImportAsync(workItem, Imported("10001", "Edited in Jira", created.AddDays(3), Priya));

        second.ShouldBe(first);
        var listed = (await store.ListAsync(workItem)).ShouldHaveSingleItem();
        listed.Body.ShouldBe("Edited in Jira");
        listed.CreatedAt.ShouldBe(created);
        listed.EditedAt.ShouldBeNull();
        listed.EditedLabel.ShouldBeNull();
    }

    [Test]
    public async Task Import_WhenArchivedThenRerun_ShouldStayHidden()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var created = new DateTimeOffset(2025, 3, 2, 9, 30, 0, TimeSpan.Zero);
        var id = await store.ImportAsync(workItem, Imported("10002", "Original", created, Priya));
        await store.ArchiveAsync(id);

        var again = await store.ImportAsync(workItem, Imported("10002", "Edited in Jira", created, Priya));

        again.ShouldBe(id);
        (await store.ListAsync(workItem)).ShouldBeEmpty();
    }

    [Test]
    public async Task Import_WhenImported_ShouldKeepTheAuthorAndCreatedTime()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var created = new DateTimeOffset(2025, 3, 2, 9, 30, 0, TimeSpan.Zero);

        var byUser = await store.ImportAsync(workItem, Imported("1", "From Priya", created, Priya));
        var byNobody = await store.ImportAsync(workItem, Imported("2", "From a stranger", created.AddHours(1), null));

        var listed = await store.ListAsync(workItem);
        listed.Select(c => c.Id).ShouldBe([byUser, byNobody]);
        listed[0].Author.ShouldNotBeNull().Id.ShouldBe(Priya);
        listed[0].CreatedAt.ShouldBe(created);
        listed[1].Author.ShouldBeNull();
    }

    [Test]
    public async Task Import_WhenSameIdOnAnotherWorkItem_ShouldBeAnotherComment()
    {
        var one = await ArrangeWorkItemAsync();
        var two = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var created = new DateTimeOffset(2025, 3, 2, 9, 30, 0, TimeSpan.Zero);

        var first = await store.ImportAsync(one, Imported("1", "Here", created, null));
        var second = await store.ImportAsync(two, Imported("1", "There", created, null));

        second.ShouldNotBe(first);
    }

    private static ImportedComment Imported(string id, string body, DateTimeOffset createdAt, Guid? author)
        => new("jira", id, author, body, createdAt);
}
