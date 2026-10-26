using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Capture;

// capture_plan end to end as Joel over a bearer: a preview with a read token, the apply with a write token, against SHIP.
[TestFixture]
public class CapturePlanTests : McpFixture
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero));

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<ScriptedPlanShapeRules>();
        services.AddSingleton<IPlanShapeRules>(sp => sp.GetRequiredService<ScriptedPlanShapeRules>());
        services.AddSingleton<AttachFailures>();
        services.AddSingleton<TreeGate>();
        services.AddScoped<IWorkItemTreeWriter>(sp => new GatedTreeWriter(ActivatorUtilities.CreateInstance<WorkItemStore>(sp), sp.GetRequiredService<TreeGate>()));
        services.AddScoped<IAttachmentStore>(sp => new FlakyAttachmentStore(sp.GetRequiredService<AttachmentStore>(), sp.GetRequiredService<AttachFailures>()));
    };

    private TreeGate Gate => Host.Services.GetRequiredService<TreeGate>();

    private ScriptedPlanShapeRules Rules => Host.Services.GetRequiredService<ScriptedPlanShapeRules>();

    [SetUp]
    public void ResetRules()
    {
        Host.Services.GetRequiredService<AttachFailures>().Arm(0);
        Gate.Arm(0);
        Rules.Findings = [];
        Host.ActAs(SeedIds.Users.Joel);
    }

    private static JsonObject Args(Action<JsonObject>? edit = null)
    {
        var arguments = PlanFixture.FeatureEditor.Arguments();
        arguments["project"] = "SHIP";
        edit?.Invoke(arguments);
        return arguments;
    }

    private async Task<JsonElement> PreviewAsync(JsonObject arguments)
        => (await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "capture_plan", arguments.ToJsonString())).Structured();

    private async Task<McpReply> ApplyAsync(JsonObject arguments, string plan)
    {
        var apply = (JsonObject)arguments.DeepClone();
        apply["plan"] = plan;
        return await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWrite), "capture_plan", apply.ToJsonString());
    }

    private async Task<JsonElement> PreviewAndApplyAsync(JsonObject arguments)
    {
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var applied = await ApplyAsync(arguments, plan);
        applied.IsToolError().ShouldBeFalse(applied.Body);
        return applied.Structured();
    }

    private static string ErrorCode(McpReply reply) => reply.Structured().GetProperty("error").GetProperty("code").GetString()!;

    private static string ErrorField(McpReply reply) => reply.Structured().GetProperty("error").GetProperty("field").GetString()!;

    private async Task<McpReply> PreviewReplyAsync(JsonObject arguments)
        => await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "capture_plan", arguments.ToJsonString());

    private async Task<int> CountItemsAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item");
    }

    private async Task<Guid> IdOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key });
    }

    private async Task<WorkItemSnapshot> SnapshotAsync(string key)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().GetAsync(await IdOfAsync(key));
    }

    private async Task<IReadOnlyList<AttachmentDto>> AttachmentsAsync(string key)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().ListAsync(await IdOfAsync(key));
    }

    private async Task<string> ContentAsync(string key, string name)
    {
        var file = (await AttachmentsAsync(key)).Single(f => f.Name == name);
        using var scope = Host.Services.CreateScope();
        var (_, content) = await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().OpenAsync(file.Id);
        await using var stream = content.Content;
        return await new StreamReader(stream).ReadToEndAsync();
    }

    private async Task<int> CountPlanStoriesAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE parent_id = @id AND type = 'story'", new { id = await IdOfAsync(key) });
    }

    // A Feature of the given type with the tags and goal a test needs, made as the person would.
    private async Task<string> CreateAsync(WorkItemTypeId type, string title, string goal = "", params string[] tags)
        => await CreateWithDescriptionAsync(type, title, goal, "", tags);

    private async Task<string> CreateWithDescriptionAsync(WorkItemTypeId type, string title, string goal, string description, params string[] tags)
    {
        using var scope = Host.Services.CreateScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().CreateAsync(
            new WorkItemCreate(SeedIds.Projects.Ship, type, title, null, null, new WorkItemChanges { Tags = new FieldSet<IReadOnlyList<string>>(tags), Description = new FieldSet<string>(description) }) { Goal = goal },
            WriteOptions.User);
        return outcome.Written!.Key;
    }

    [Test]
    public async Task CapturePlan_FeatureEditorFixturePreviewThenApply_ShouldCreateTheWholeTree()
    {
        var fixture = PlanFixture.FeatureEditor;
        var before = await CountItemsAsync();

        var preview = await PreviewAsync(Args());

        preview.GetProperty("applied").GetBoolean().ShouldBeFalse();
        preview.GetProperty("feature").GetString().ShouldBe(fixture.Feature.Title);
        preview.GetProperty("items").GetInt32().ShouldBe(fixture.ItemCount);
        preview.GetProperty("stories").GetArrayLength().ShouldBe(fixture.Stories.Count);
        preview.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        preview.GetProperty("expires_in_minutes").GetInt32().ShouldBe(15);
        (await CountItemsAsync()).ShouldBe(before);

        var applied = await PreviewAndApplyAsync(Args());

        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        var stories = applied.GetProperty("stories").EnumerateArray().ToList();
        stories.Select(s => s.GetProperty("title").GetString()).ShouldBe(fixture.Stories.Select(s => s.Title));
        stories.Sum(s => s.GetProperty("tasks").GetArrayLength()).ShouldBe(fixture.ItemCount - 1 - fixture.Stories.Count);
        (await CountItemsAsync()).ShouldBe(before + fixture.ItemCount);
        var feature = await SnapshotAsync(applied.GetProperty("feature").GetString()!);
        feature.Type.ShouldBe(WorkItemTypeId.Feature);
        feature.Title.ShouldBe(fixture.Feature.Title);
        feature.Goal.ShouldBe(fixture.Feature.Goal);
        feature.Description.ShouldBe(fixture.Feature.Description);
    }

    [Test]
    public async Task CapturePlan_Apply_ShouldTagWaveLaneModelCtxAndRankStoriesByWave()
    {
        var fixture = PlanFixture.FeatureEditor;
        var pointed = fixture.Stories[^1].Tasks[0].Title;

        // Sent in reverse; the stable sort by wave keeps lanes L, A, F of wave 1 in the order they were sent (the reverse of the fixture).
        var applied = await PreviewAndApplyAsync(Args(a =>
        {
            a["stories"] = new JsonArray([.. ((JsonArray)a["stories"]!).Reverse().Select(s => s!.DeepClone())]);
            ((JsonObject)((JsonArray)((JsonObject)((JsonArray)a["stories"]!)[0]!)["tasks"]!)[0]!)["points"] = 3;
        }));

        var stories = applied.GetProperty("stories").EnumerateArray().ToList();
        stories.Select(s => s.GetProperty("wave").GetString()).ShouldBe(["0.1", "0.2", "1", "1", "1", "2"]);
        stories.Select(s => s.GetProperty("lane").GetString()).Skip(2).Take(3).ShouldBe(fixture.Stories.Where(s => s.Wave.Text == "1").Select(s => s.Lane).Reverse());
        var keys = stories.Select(s => s.GetProperty("key").GetString()!).ToList();
        await using (var connection = await OpenDatabaseAsync())
        {
            var ranks = (await connection.QueryAsync<(string Key, string Order)>("SELECT key, backlog_order FROM work_item WHERE key = ANY(@keys)", new { keys })).ToList();
            ranks.Select(r => r.Order).Distinct().Count().ShouldBe(keys.Count);
            ranks.OrderBy(r => r.Order, StringComparer.Ordinal).Select(r => r.Key).ShouldBe(keys);
        }

        (await SnapshotAsync(applied.GetProperty("feature").GetString()!)).Tags.ShouldBe([PlanTags.Branch + fixture.SharedBranch]);
        foreach (var story in stories)
        {
            var planned = fixture.Stories.Single(s => s.Title == story.GetProperty("title").GetString());
            var storySnapshot = await SnapshotAsync(story.GetProperty("key").GetString()!);
            PlanTags.Parse(storySnapshot.Tags).ShouldBe(planned.Tags);
            storySnapshot.Description.ShouldBe(planned.Description);
            foreach (var task in story.GetProperty("tasks").EnumerateArray())
            {
                var expected = planned.Tasks.Single(t => t.Title == task.GetProperty("title").GetString());
                var snapshot = await SnapshotAsync(task.GetProperty("key").GetString()!);
                PlanTags.Parse(snapshot.Tags).ShouldBe(expected.Tags);
                snapshot.Description.ShouldBe(expected.Brief);
                snapshot.Points.ShouldBe(expected.Title == pointed ? 3m : null);
                snapshot.ParentId.ShouldBe(await IdOfAsync(story.GetProperty("key").GetString()!));
            }
        }
    }

    [Test]
    public async Task CapturePlan_Apply_ShouldAttachCommonBriefAndPlan()
    {
        var applied = await PreviewAndApplyAsync(Args());

        applied.GetProperty("attachments").EnumerateArray().Select(a => a.GetString()).ShouldBe([PlanFiles.CommonBrief, PlanFiles.Plan]);
        var files = await AttachmentsAsync(applied.GetProperty("feature").GetString()!);
        files.Select(f => f.Name).Order().ShouldBe([PlanFiles.CommonBrief, PlanFiles.Plan]);
        files.ShouldAllBe(f => f.Via == "capture_plan" && f.ContentType == "text/markdown" && f.AgentName != null);
        var key = applied.GetProperty("feature").GetString()!;
        (await ContentAsync(key, PlanFiles.CommonBrief)).ShouldBe(PlanFixture.FeatureEditor.CommonBrief);
        (await ContentAsync(key, PlanFiles.Plan)).ShouldBe(PlanFixture.FeatureEditor.PlanMarkdown);
    }

    [Test]
    public async Task CapturePlan_ExistingFeature_ShouldReplaceAttachmentsAndWriteGoal()
    {
        var key = await CreateWithDescriptionAsync(WorkItemTypeId.Feature, "Bare feature", "Old goal", "Old description.", "branch:feat/old", "keep-me");
        using (var scope = Host.Services.CreateScope())
        {
            await using var old = new MemoryStream("Old brief."u8.ToArray());
            await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().AddAsync(await IdOfAsync(key), PlanFiles.CommonBrief, "text/markdown", old);
        }

        var arguments = Args(a =>
        {
            a["feature"] = new JsonObject { ["key"] = key, ["title"] = "A new title", ["goal"] = "New goal." };
            a["shared_branch"] = "feat/new";
            a["common_brief"] = "New brief.";
        });
        var preview = await PreviewAsync(arguments);
        preview.GetProperty("feature").GetString().ShouldBe(key);
        preview.GetProperty("items").GetInt32().ShouldBe(PlanFixture.FeatureEditor.ItemCount - 1);
        var applied = await PreviewAndApplyAsync(arguments);

        applied.GetProperty("feature").GetString().ShouldBe(key);
        var feature = await SnapshotAsync(key);
        feature.Goal.ShouldBe("New goal.");
        feature.Title.ShouldBe("Bare feature");
        feature.Description.ShouldBe("Old description.");
        feature.Tags.Where(t => t.StartsWith(PlanTags.Branch, StringComparison.Ordinal)).ShouldBe(["branch:feat/new"]);
        feature.Tags.ShouldContain("keep-me");
        var files = await AttachmentsAsync(key);
        files.Select(f => f.Name).Order().ShouldBe([PlanFiles.CommonBrief, PlanFiles.Plan]);
        files.Single(f => f.Name == PlanFiles.CommonBrief).ReplacedCount.ShouldBe(1);
        (await ContentAsync(key, PlanFiles.CommonBrief)).ShouldBe("New brief.");
        applied.GetProperty("stories").GetArrayLength().ShouldBe(PlanFixture.FeatureEditor.Stories.Count);
        (await SnapshotAsync(applied.GetProperty("stories")[0].GetProperty("key").GetString()!)).ParentId.ShouldBe(feature.Id);
    }

    [Test]
    public async Task CapturePlan_ExistingFeatureWithDescription_ShouldOverwriteIt()
    {
        var key = await CreateWithDescriptionAsync(WorkItemTypeId.Feature, "Bare feature", "Old goal", "Old description.");

        await PreviewAndApplyAsync(Args(a => a["feature"] = new JsonObject { ["key"] = key, ["description"] = "New description." }));

        var feature = await SnapshotAsync(key);
        feature.Description.ShouldBe("New description.");
        feature.Goal.ShouldBe("Old goal");
    }

    [Test]
    public async Task CapturePlan_ApplyWithChangedArguments_ShouldBeRefused()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var before = await CountItemsAsync();

        var changed = (JsonObject)arguments.DeepClone();
        changed["common_brief"] = "A different brief.";
        var refused = await ApplyAsync(changed, plan);

        refused.IsToolError().ShouldBeTrue();
        ErrorCode(refused).ShouldBe(ApiErrorCodes.Validation);
        ErrorField(refused).ShouldBe("plan");
        refused.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain("arguments changed since the preview");
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CapturePlan_ApplyWithChangedNestedArgument_ShouldBeRefusedAsPlan()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var before = await CountItemsAsync();
        var edits = new Action<JsonObject>[]
        {
            a => ((JsonObject)((JsonArray)((JsonObject)((JsonArray)a["stories"]!)[0]!)["tasks"]!)[0]!)["brief"] = "A different brief.",
            a => a["plan_markdown"] = "A different plan.",
        };

        foreach (var edit in edits)
        {
            var changed = (JsonObject)arguments.DeepClone();
            edit(changed);
            var refused = await ApplyAsync(changed, plan);

            ErrorCode(refused).ShouldBe(ApiErrorCodes.Validation);
            ErrorField(refused).ShouldBe("plan");
        }

        (await CountItemsAsync()).ShouldBe(before);
    }

    // Refused by the digest (feature.key is a digest field), not by the seal's FeatureId, which is defence in depth here.
    [Test]
    public async Task CapturePlan_NewFeaturePlanAppliedToABareFeature_ShouldBeRefusedAsPlan()
    {
        var bare = await CreateAsync(WorkItemTypeId.Feature, "Bare feature");
        var plan = (await PreviewAsync(Args())).GetProperty("plan").GetString()!;

        var refused = await ApplyAsync(Args(a => a["feature"] = new JsonObject { ["key"] = bare }), plan);

        ErrorCode(refused).ShouldBe(ApiErrorCodes.Validation);
        ErrorField(refused).ShouldBe("plan");
        (await CountPlanStoriesAsync(bare)).ShouldBe(0);
    }

    [Test]
    public async Task CapturePlan_LaneWithSpaces_ShouldBeStoredTrimmed()
    {
        var applied = await PreviewAndApplyAsync(Args(a => ((JsonObject)((JsonArray)a["stories"]!)[2]!)["lane"] = "  F  "));

        var story = applied.GetProperty("stories")[2];
        story.GetProperty("lane").GetString().ShouldBe("F");
        PlanTags.Parse((await SnapshotAsync(story.GetProperty("key").GetString()!)).Tags).Lane.ShouldBe("F");
        PlanTags.Parse((await SnapshotAsync(story.GetProperty("tasks")[0].GetProperty("key").GetString()!)).Tags).Lane.ShouldBe("F");
    }

    [Test]
    public async Task CapturePlan_NullFeature_ShouldBeValidation()
    {
        var reply = await PreviewReplyAsync(Args(a => a["feature"] = null));

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
        ErrorField(reply).ShouldBe("feature");
    }

    [Test]
    public async Task CapturePlan_ApplyTwice_ShouldWriteOnce()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var first = (await ApplyAsync(arguments, plan)).Structured();
        var after = await CountItemsAsync();

        var second = await ApplyAsync(arguments, plan);

        second.IsToolError().ShouldBeFalse(second.Body);
        var again = second.Structured();
        again.GetProperty("feature").GetString().ShouldBe(first.GetProperty("feature").GetString());
        again.GetProperty("stories").EnumerateArray().Select(s => s.GetProperty("key").GetString()).ShouldBe(first.GetProperty("stories").EnumerateArray().Select(s => s.GetProperty("key").GetString()));
        again.GetProperty("attachments").EnumerateArray().Select(a => a.GetString()).ShouldBe([PlanFiles.CommonBrief, PlanFiles.Plan]);
        (await CountItemsAsync()).ShouldBe(after);
        (await CountPlanStoriesAsync(again.GetProperty("feature").GetString()!)).ShouldBe(PlanFixture.FeatureEditor.Stories.Count);
    }

    [Test]
    public async Task CapturePlan_RetryAfterFailedAttach_ShouldAttachWithoutDuplicating()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        Host.Services.GetRequiredService<AttachFailures>().Arm(1);

        var failed = await ApplyAsync(arguments, plan);

        failed.IsToolError().ShouldBeTrue();
        var created = await CountItemsAsync();
        var retried = await ApplyAsync(arguments, plan);

        retried.IsToolError().ShouldBeFalse(retried.Body);
        var key = retried.Structured().GetProperty("feature").GetString()!;
        (await CountItemsAsync()).ShouldBe(created);
        (await CountPlanStoriesAsync(key)).ShouldBe(PlanFixture.FeatureEditor.Stories.Count);
        (await ContentAsync(key, PlanFiles.CommonBrief)).ShouldBe(PlanFixture.FeatureEditor.CommonBrief);
        (await ContentAsync(key, PlanFiles.Plan)).ShouldBe(PlanFixture.FeatureEditor.PlanMarkdown);
    }

    [Test]
    public async Task CapturePlan_RetryAfterFailedAttachOnExistingFeature_ShouldAttachWithoutDuplicating()
    {
        var key = await CreateAsync(WorkItemTypeId.Feature, "Bare feature");
        var arguments = Args(a => a["feature"] = new JsonObject { ["key"] = key });
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        Host.Services.GetRequiredService<AttachFailures>().Arm(1);

        (await ApplyAsync(arguments, plan)).IsToolError().ShouldBeTrue();
        var retried = await ApplyAsync(arguments, plan);

        retried.IsToolError().ShouldBeFalse(retried.Body);
        retried.Structured().GetProperty("feature").GetString().ShouldBe(key);
        (await CountPlanStoriesAsync(key)).ShouldBe(PlanFixture.FeatureEditor.Stories.Count);
        (await ContentAsync(key, PlanFiles.CommonBrief)).ShouldBe(PlanFixture.FeatureEditor.CommonBrief);
    }

    // Two applies of one token, held at the tree write until both are in, so both passed the claim check before either committed.
    private async Task<McpReply[]> ApplyTwoAtOnceAsync(JsonObject arguments, string firstPlan, string secondPlan)
    {
        Gate.Arm(2);
        var first = ApplyAsync(arguments, firstPlan);
        await Gate.EnteredAsync(1);
        var second = ApplyAsync(arguments, secondPlan);
        return await Task.WhenAll(first, second);
    }

    [Test]
    public async Task CapturePlan_TwoConcurrentAppliesOfOneToken_ShouldBothReturnThePlan()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var before = await CountItemsAsync();

        var replies = await ApplyTwoAtOnceAsync(arguments, plan, plan);

        replies.ShouldAllBe(r => !r.IsToolError(), string.Join(" | ", replies.Select(r => r.Body)));
        Keys(replies[1].Structured()).ShouldBe(Keys(replies[0].Structured()));
        (await CountItemsAsync()).ShouldBe(before + PlanFixture.FeatureEditor.ItemCount);
        (await CountClaimsAsync(replies[0].Structured().GetProperty("feature").GetString()!)).ShouldBe(1);
    }

    [Test]
    public async Task CapturePlan_TwoConcurrentAppliesOfOneTokenOnAnExistingFeature_ShouldBothReturnThePlan()
    {
        var key = await CreateAsync(WorkItemTypeId.Feature, "Bare feature");
        var arguments = Args(a => a["feature"] = new JsonObject { ["key"] = key });
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;

        var replies = await ApplyTwoAtOnceAsync(arguments, plan, plan);

        replies.ShouldAllBe(r => !r.IsToolError(), string.Join(" | ", replies.Select(r => r.Body)));
        Keys(replies[1].Structured()).ShouldBe(Keys(replies[0].Structured()));
        (await CountPlanStoriesAsync(key)).ShouldBe(PlanFixture.FeatureEditor.Stories.Count);
        (await CountClaimsAsync(key)).ShouldBe(1);
    }

    [Test]
    public async Task CapturePlan_TwoPreviewsRacingOnOneFeature_ShouldLeaveOnePlan()
    {
        var key = await CreateAsync(WorkItemTypeId.Feature, "Bare feature");
        var arguments = Args(a => a["feature"] = new JsonObject { ["key"] = key });
        var first = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var second = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;

        var replies = await ApplyTwoAtOnceAsync(arguments, first, second);

        replies.Count(r => !r.IsToolError()).ShouldBe(1, string.Join(" | ", replies.Select(r => r.Body)));
        var refused = replies.Single(r => r.IsToolError());
        ErrorCode(refused).ShouldBe(ApiErrorCodes.Validation, refused.Body);
        ErrorField(refused).ShouldBe("feature.key");
        (await CountPlanStoriesAsync(key)).ShouldBe(PlanFixture.FeatureEditor.Stories.Count);
        (await CountClaimsAsync(key)).ShouldBe(1);
    }

    [Test]
    public async Task CapturePlan_TwoPlansRacingOnOneFeature_ShouldLeaveTheWinnersBranchAndDescription()
    {
        var key = await CreateAsync(WorkItemTypeId.Feature, "Bare feature");
        JsonObject Variant(string branch) => Args(a =>
        {
            a["feature"] = new JsonObject { ["key"] = key, ["description"] = $"Plan on {branch}." };
            a["shared_branch"] = branch;
        });
        var first = Variant("feat/first");
        var second = Variant("feat/second");
        var firstPlan = (await PreviewAsync(first)).GetProperty("plan").GetString()!;
        var secondPlan = (await PreviewAsync(second)).GetProperty("plan").GetString()!;

        Gate.Arm(2);
        var firstReply = ApplyAsync(first, firstPlan);
        await Gate.EnteredAsync(1);
        var secondReply = ApplyAsync(second, secondPlan);
        var replies = await Task.WhenAll(firstReply, secondReply);

        replies.Count(r => !r.IsToolError()).ShouldBe(1, string.Join(" | ", replies.Select(r => r.Body)));
        var winner = replies[0].IsToolError() ? "feat/second" : "feat/first";
        var feature = await SnapshotAsync(key);
        PlanTags.Parse(feature.Tags).Branch.ShouldBe(winner);
        feature.Description.ShouldBe($"Plan on {winner}.");
    }

    [Test]
    public async Task CapturePlan_RetryAfterAPersonReplacedTheCommonBrief_ShouldKeepTheirFile()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var key = (await ApplyAsync(arguments, plan)).Structured().GetProperty("feature").GetString()!;
        using (var scope = Host.Services.CreateScope())
        {
            await using var edited = new MemoryStream("Edited by a person."u8.ToArray());
            await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().AddAsync(await IdOfAsync(key), PlanFiles.CommonBrief, "text/markdown", edited);
        }

        var retried = await ApplyAsync(arguments, plan);

        retried.IsToolError().ShouldBeFalse(retried.Body);
        (await ContentAsync(key, PlanFiles.CommonBrief)).ShouldBe("Edited by a person.");
        (await ContentAsync(key, PlanFiles.Plan)).ShouldBe(PlanFixture.FeatureEditor.PlanMarkdown);
    }

    [Test]
    public async Task CapturePlan_PlanWithACapitalPAndAReadToken_ShouldOnlyPreview()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var before = await CountItemsAsync();
        var sneaky = (JsonObject)arguments.DeepClone();
        sneaky["Plan"] = plan;

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "capture_plan", sneaky.ToJsonString());

        // The binding is case-sensitive, so "Plan" is not the plan: the call is a preview, which a read token may make.
        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("applied").GetBoolean().ShouldBeFalse();
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CapturePlan_NullStories_ShouldBeValidation()
    {
        var reply = await PreviewReplyAsync(Args(a => a["stories"] = null));

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation, reply.Body);
        ErrorField(reply).ShouldBe("stories");
    }

    // A backlog whose top key is nearly the longest allowed, so the tree's own creates renumber the whole backlog: every
    // Feature row is then rewritten by the apply's transaction, not only the new one.
    [Test]
    public async Task CapturePlan_ApplyThatRenumbersTheBacklog_ShouldClaimTheNewFeature()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Deep top").WithBacklogOrder(new string('0', 21) + "1").InsertAsync(connection);
        }
        var arguments = Args();

        var applied = await PreviewAndApplyAsync(arguments);

        var key = applied.GetProperty("feature").GetString()!;
        (await CountClaimsAsync(key)).ShouldBe(1);
    }

    [Test]
    public async Task CapturePlan_RetryAfterTheFeatureWasRemoved_ShouldBeValidationAndAttachNothing()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var key = (await ApplyAsync(arguments, plan)).Structured().GetProperty("feature").GetString()!;
        var attached = (await AttachmentsAsync(key)).Count;
        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().RemoveAsync(await IdOfAsync(key), (await SnapshotAsync(key)).Revision);
        }

        var retry = await ApplyAsync(arguments, plan);

        ErrorCode(retry).ShouldBe(ApiErrorCodes.Validation, retry.Body);
        ErrorField(retry).ShouldBe("feature.key");
        (await AttachmentsAsync(key)).Count.ShouldBe(attached);
    }

    private static IEnumerable<string?> Keys(JsonElement applied)
        => applied.GetProperty("stories").EnumerateArray().Select(s => s.GetProperty("key").GetString()).Prepend(applied.GetProperty("feature").GetString());

    private async Task<int> CountClaimsAsync(string featureKey)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM mcp_capture_plan_apply WHERE feature_id = @id", new { id = await IdOfAsync(featureKey) });
    }

    [Test]
    public async Task CapturePlan_ExpiredToken_ShouldBeConflict()
    {
        var arguments = Args();
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        _clock.Advance(PlanEnvelope.Lifetime + TimeSpan.FromSeconds(1));
        var before = await CountItemsAsync();

        var apply = (JsonObject)arguments.DeepClone();
        apply["plan"] = plan;
        var reply = await CallToolAsync(token, "capture_plan", apply.ToJsonString());

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Conflict);
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CapturePlan_RuleError_ShouldRefusePreview()
    {
        Rules.Findings = [new PlanFinding(PlanFindingSeverity.Error, "stories[2].tasks[0].context_k", "Too big."), new PlanFinding(PlanFindingSeverity.Warning, "stories[0].title", "Vague.")];
        var before = await CountItemsAsync();

        var reply = await PreviewReplyAsync(Args());

        reply.IsToolError().ShouldBeTrue();
        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
        ErrorField(reply).ShouldBe("stories[2].tasks[0].context_k");
        reply.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain("Too big.");
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CapturePlan_RuleWarning_ShouldRideOnPreview()
    {
        Rules.Findings = [new PlanFinding(PlanFindingSeverity.Warning, "stories[0].title", "Vague.")];

        var preview = await PreviewAsync(Args());

        var warning = preview.GetProperty("warnings").EnumerateArray().Single();
        warning.GetProperty("field").GetString().ShouldBe("stories[0].title");
        warning.GetProperty("message").GetString().ShouldBe("Vague.");
        preview.GetProperty("plan").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task CapturePlan_RemovedOrNonFeatureKey_ShouldBeValidation()
    {
        var story = await CreateAsync(WorkItemTypeId.Story, "Not a feature");
        var removed = await CreateAsync(WorkItemTypeId.Feature, "Removed feature");
        var abandoned = await CreateAsync(WorkItemTypeId.Feature, "Removed after preview");
        using var scope = Host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkItemStore>();
        await store.RemoveAsync(await IdOfAsync(removed), (await SnapshotAsync(removed)).Revision);
        var arguments = Args(a => a["feature"] = new JsonObject { ["key"] = abandoned, ["title"] = "x", ["goal"] = "y" });
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        await store.RemoveAsync(await IdOfAsync(abandoned), (await SnapshotAsync(abandoned)).Revision);

        foreach (var key in new[] { story, removed })
        {
            var reply = await PreviewReplyAsync(Args(a => a["feature"] = new JsonObject { ["key"] = key, ["title"] = "x", ["goal"] = "y" }));
            ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation, key);
            ErrorField(reply).ShouldBe("feature.key");
        }

        var late = await ApplyAsync(arguments, plan);
        ErrorCode(late).ShouldBe(ApiErrorCodes.Validation);
        ErrorField(late).ShouldBe("feature.key");
    }

    [Test]
    public async Task CapturePlan_FeatureWithAPlan_ShouldBeValidation()
    {
        var planned = (await PreviewAndApplyAsync(Args())).GetProperty("feature").GetString()!;
        var bare = await CreateAsync(WorkItemTypeId.Feature, "Planned twice");
        var arguments = Args(a => a["feature"] = new JsonObject { ["key"] = bare, ["title"] = "x", ["goal"] = "y" });
        var plan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        var otherPlan = (await PreviewAsync(arguments)).GetProperty("plan").GetString()!;
        (await ApplyAsync(arguments, plan)).IsToolError().ShouldBeFalse();

        var again = await PreviewReplyAsync(Args(a => a["feature"] = new JsonObject { ["key"] = planned, ["title"] = "x", ["goal"] = "y" }));
        var twice = await ApplyAsync(arguments, otherPlan);

        foreach (var reply in new[] { again, twice })
        {
            ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
            ErrorField(reply).ShouldBe("feature.key");
            reply.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain("already has a plan");
        }
    }
}
