using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

[TestFixture]
public class PlanSprintTests : SprintFixture
{
    private static string Preview(string sprint, string extra = "") => $$"""{"project":"EMPT","sprint":"{{sprint}}"{{extra}}}""";

    private static string Apply(string plan) => $$"""{"project":"EMPT","plan":"{{plan}}"}""";

    [Test]
    public async Task PlanSprint_WhenNoPlan_ShouldOnlyPropose()
    {
        await ParkBacklogAsync();
        await AddSprintAsync("Propose", 1, 14, capacity: 8);
        var a = await AddStoryAsync("Propose a", 3);
        var b = await AddStoryAsync("Propose b", 3);
        var c = await AddStoryAsync("Propose c", 3);
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "plan_sprint", Preview("Propose"));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        result.GetProperty("applied").GetBoolean().ShouldBeFalse();
        result.GetProperty("room").GetDecimal().ShouldBe(8m);
        result.GetProperty("room_source").GetString().ShouldBe("capacity");
        Keys(result).ShouldBe([await KeyOfAsync(a), await KeyOfAsync(b)]);
        result.GetProperty("next_in_stack")[0].GetProperty("key").GetString().ShouldBe(await KeyOfAsync(c));
        result.GetProperty("plan").GetString().ShouldNotBeNullOrEmpty();
        (await SprintOfAsync(a)).ShouldBeNull();
        (await SprintOfAsync(b)).ShouldBeNull();
    }

    [Test]
    public async Task PlanSprint_WhenReadWriteToken_ShouldPreviewButAnswer403OnApply()
    {
        await ParkBacklogAsync();
        var sprint = await AddSprintAsync("Scoped", 1, 14, capacity: 8);
        var a = await AddStoryAsync("Scoped a", 3);
        var readWrite = await TokenAsync(TestUser.Joel, ReadWrite);

        var preview = await CallToolAsync(readWrite, "plan_sprint", Preview("Scoped"));
        preview.IsToolError().ShouldBeFalse(preview.Body);
        var plan = preview.Structured().GetProperty("plan").GetString()!;
        var refused = await CallToolAsync(readWrite, "plan_sprint", Apply(plan));

        refused.Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SprintOfAsync(a)).ShouldBeNull();

        var planToken = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var applied = await CallToolAsync(planToken, "plan_sprint", Apply(plan));

        applied.IsToolError().ShouldBeFalse(applied.Body);
        applied.Structured().GetProperty("applied").GetBoolean().ShouldBeTrue();
        Keys(applied.Structured()).ShouldBe([await KeyOfAsync(a)]);
        (await SprintOfAsync(a)).ShouldBe(sprint);
    }

    [Test]
    public async Task PlanSprint_WhenBacklogChangedSincePreview_ShouldRefuse()
    {
        await ParkBacklogAsync();
        await AddSprintAsync("Changed", 1, 14, capacity: 8);
        var a = await AddStoryAsync("Changed a", 3);
        var b = await AddStoryAsync("Changed b", 3);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var plan = (await CallToolAsync(token, "plan_sprint", Preview("Changed"))).Structured().GetProperty("plan").GetString()!;
        await using (var connection = await OpenDatabaseAsync())
        {
            await Dapper.SqlMapper.ExecuteAsync(connection, "UPDATE work_item SET revision = revision + 1 WHERE id = @b", new { b });
        }

        var reply = await CallToolAsync(token, "plan_sprint", Apply(plan));

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Conflict);
        (await SprintOfAsync(a)).ShouldBeNull();
        (await SprintOfAsync(b)).ShouldBeNull();
    }

    [Test]
    public async Task PlanSprint_WhenOverCapacity_ShouldWarnNotBlock()
    {
        await ParkBacklogAsync();
        var sprint = await AddSprintAsync("Over", 1, 14, capacity: 2);
        var big = await AddStoryAsync("Over big", 5);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var preview = await CallToolAsync(token, "plan_sprint", Preview("Over"));

        preview.IsToolError().ShouldBeFalse(preview.Body);
        Keys(preview.Structured()).ShouldBe([await KeyOfAsync(big)]);
        preview.Structured().GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldContain(w => w!.Contains("over"));

        var applied = await CallToolAsync(token, "plan_sprint", Apply(preview.Structured().GetProperty("plan").GetString()!));

        applied.IsToolError().ShouldBeFalse(applied.Body);
        (await SprintOfAsync(big)).ShouldBe(sprint);
    }

    [Test]
    public async Task PlanSprint_WhenNoCapacityNoVelocity_ShouldNeedPointsLimit()
    {
        await ParkBacklogAsync();
        var sprint = await AddSprintAsync("First", 1, 14);
        var a = await AddStoryAsync("First a", 3);
        var b = await AddStoryAsync("First b", 3);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var bare = await CallToolAsync(token, "plan_sprint", Preview("First"));

        bare.IsToolError().ShouldBeFalse(bare.Body);
        var result = bare.Structured();
        result.GetProperty("needs_points_limit").GetBoolean().ShouldBeTrue();
        result.GetProperty("items").GetArrayLength().ShouldBe(0);
        result.GetProperty("plan").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        result.GetProperty("next_in_stack").GetArrayLength().ShouldBe(2);

        var limited = await CallToolAsync(token, "plan_sprint", Preview("First", ""","points_limit":3"""));

        limited.IsToolError().ShouldBeFalse(limited.Body);
        limited.Structured().GetProperty("room_source").GetString().ShouldBe("points_limit");
        Keys(limited.Structured()).ShouldBe([await KeyOfAsync(a)]);
        var applied = await CallToolAsync(token, "plan_sprint", Apply(limited.Structured().GetProperty("plan").GetString()!));
        applied.IsToolError().ShouldBeFalse(applied.Body);
        (await SprintOfAsync(a)).ShouldBe(sprint);
        (await SprintOfAsync(b)).ShouldBeNull();
    }
}
