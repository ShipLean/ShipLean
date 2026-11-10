using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Asks;

// Quick-answer options on a question: asked through IQuestionWrites as Joel (no REST route asks), read back over
// GET api/work-items/{id}/asks.
[TestFixture]
public class QuestionOptionsTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private async Task<Guid> NewItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).WithAssignee(SeedIds.Users.Joel).InsertAsync(connection);
    }

    private async Task AskAsync(Guid item, string[]? options)
    {
        Host.ActAs(SeedIds.Users.Joel);
        try
        {
            await using var scope = Host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IQuestionWrites>().AskAsync(item, "Which one?", options);
        }
        finally
        {
            Host.ActAs(null);
        }
    }

    private async Task<IReadOnlyList<QuestionDto>> ReadQuestionsAsync(Guid item)
    {
        using var response = await Api.GetAsync($"api/work-items/{item}/asks");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<ItemAsksDto>(text, WireOptions)!.Questions;
    }

    private async Task<(ApiException Thrown, int Stored)> RefusedAsync(string[] options)
    {
        var item = await NewItemAsync();
        var thrown = await Should.ThrowAsync<ApiException>(() => AskAsync(item, options));
        await using var connection = await OpenDatabaseAsync();
        return (thrown, await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM question WHERE work_item_id = @item", new { item }));
    }

    [Test]
    public async Task AskAsync_WhenOptionsGiven_ShouldStoreAndReadThemInOrder()
    {
        var item = await NewItemAsync();

        await AskAsync(item, ["  Yellow ", "Blue", "Red", "A", "B", "C"]);

        (await ReadQuestionsAsync(item)).ShouldHaveSingleItem().Options.ShouldBe(["Yellow", "Blue", "Red", "A", "B", "C"]);
    }

    [Test]
    public async Task AskAsync_WhenSevenOptions_ShouldReturnValidationOnOptions()
    {
        var (thrown, stored) = await RefusedAsync(["1", "2", "3", "4", "5", "6", "7"]);
        (thrown.Code, thrown.Field, stored).ShouldBe((ApiErrorCodes.Validation, "options", 0));
    }

    [Test]
    public async Task AskAsync_WhenOptionBlank_ShouldReturnValidationOnOptions()
    {
        var (thrown, stored) = await RefusedAsync(["Yes", "   "]);
        (thrown.Code, thrown.Field, stored).ShouldBe((ApiErrorCodes.Validation, "options", 0));
    }

    [Test]
    public async Task AskAsync_WhenOptionsDuplicateIgnoringCase_ShouldReturnValidationOnOptions()
    {
        var (thrown, stored) = await RefusedAsync(["Yes", " yes "]);
        (thrown.Code, thrown.Field, stored).ShouldBe((ApiErrorCodes.Validation, "options", 0));
    }

    [Test]
    public async Task AskAsync_WhenNoOptions_ShouldReadEmptyList()
    {
        var item = await NewItemAsync();

        await AskAsync(item, null);
        await AskAsync(item, []);

        (await ReadQuestionsAsync(item)).Select(q => q.Options.Count).ShouldBe([0, 0]);
    }

    [Test]
    public async Task AskAsync_WhenRefused_ShouldStoreNoQuestion()
    {
        var item = await NewItemAsync();
        await AskAsync(item, ["Keep"]);

        await Should.ThrowAsync<ApiException>(() => AskAsync(item, ["Same", "same"]));

        var questions = await ReadQuestionsAsync(item);
        (questions.Count, questions[0].Number).ShouldBe((1, 1));
    }
}
