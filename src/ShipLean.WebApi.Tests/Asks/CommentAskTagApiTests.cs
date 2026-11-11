using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Comments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Asks;

// Every comment read carries its askTag, read from comment_ask_tag. Tags are arranged with AskTagBuilder on comments from CommentBuilder.
[TestFixture]
public class CommentAskTagApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Test]
    public async Task Comments_WhenTaggedChanged_ShouldCarryD3Changed()
    {
        var (item, comment) = await TaggedAsync("changed");

        var read = (await CommentsAsync(item)).Single(c => c.Id == comment);

        (read.AskTag!.Kind, read.AskTag.Label).ShouldBe((AskTagKind.Changed, "D3"));
    }

    [Test]
    public async Task Comments_WhenTaggedAbout_ShouldCarryAboutD3()
    {
        var (item, comment) = await TaggedAsync("about");

        var read = (await CommentsAsync(item)).Single(c => c.Id == comment);

        (read.AskTag!.Kind, read.AskTag.Label).ShouldBe((AskTagKind.About, "D3"));
    }

    [Test]
    public async Task Comments_WhenPlain_ShouldHaveNoAskTag()
    {
        var (item, _) = await TaggedAsync("about");
        await using var connection = await OpenDatabaseAsync();
        var plain = await new CommentBuilder(item).InsertAsync(connection);

        var read = (await CommentsAsync(item)).Single(c => c.Id == plain);

        read.AskTag.ShouldBeNull();
    }

    private async Task<(Guid Item, Guid Comment)> TaggedAsync(string kind)
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        await new DecisionBuilder(item).Number(1).BuildAsync(connection);
        await new DecisionBuilder(item).Number(2).BuildAsync(connection);
        var (decision, _) = await new DecisionBuilder(item).Number(3).BuildAsync(connection);
        var comment = await new CommentBuilder(item).InsertAsync(connection);
        await new AskTagBuilder().Comment(comment).Kind(kind).Decision(decision).BuildAsync(connection);
        return (item, comment);
    }

    private async Task<IReadOnlyList<CommentDto>> CommentsAsync(Guid item)
    {
        using var response = await Api.GetAsync($"api/work-items/{item}/comments");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<IReadOnlyList<CommentDto>>(text, WireOptions)!;
    }
}
