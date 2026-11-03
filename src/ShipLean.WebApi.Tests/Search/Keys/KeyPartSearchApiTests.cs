using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Keys;

// GET /api/search in typing mode over the real legs: a query that is part of a key finds the items by their keys. The class shares
// one database, so every test and test case picks its own item numbers and the items of one never match the query of another.
[TestFixture]
public class KeyPartSearchApiTests : ApiFixture
{
    [TestCase(74350, "74350")]
    [TestCase(74351, "SHIP-74351")]
    [TestCase(74352, "ship74352")]
    public async Task Search_WhenWholeKeyOrNumberTyped_ShouldListThatItemFirst(int number, string query)
    {
        await using var connection = await OpenDatabaseAsync();
        var named = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, number, "Named by its number");
        var longer = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, (number * 10) + 1, "A longer number");
        var title = $"Mentions {number} in the title";
        var mentions = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle(title).InsertAsync(connection);
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Item, mentions, SearchField.Title, title).WithWorkItem(mentions).InsertAsync(connection);

        var keys = await KeysAsync(query);

        keys[0].ShouldBe(named);
        keys.ShouldContain(longer);
    }

    [Test]
    public async Task Search_WhenPartOfANumberTyped_ShouldRankWholeThenStartingThenContaining()
    {
        await using var connection = await OpenDatabaseAsync();
        var containing = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 186201, "Contains the part");
        var starting = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 862019, "Starts with the part");
        var whole = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 86201, "Is the part");

        (await KeysAsync("86201")).ShouldBe([whole, starting, containing]);
    }

    [TestCase(71209, "SHIP-7120")]
    [TestCase(71219, "HIP-7121")]
    [TestCase(71229, "hip7122")]
    public async Task Search_WhenPartOfAKeyTyped_ShouldFindIt(int number, string query)
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, number, "Found by part of its key");

        (await KeysAsync(query)).ShouldContain(item);
    }

    [Test]
    public async Task Search_WhenNumberNamesAnItemInTwoProjects_ShouldListBoth()
    {
        await using var connection = await OpenDatabaseAsync();
        var inShip = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 65431, "The number in SHIP");
        var inPay = await ArrangeNumberedAsync(connection, SeedIds.Projects.Pay, 65431, "The number in PAY");
        var longer = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 654319, "A longer number");

        var keys = await KeysAsync("65431");

        keys.Take(2).ShouldBe([inShip, inPay], ignoreOrder: true);
        keys[2].ShouldBe(longer);
    }

    [Test]
    public async Task Search_WhenNumberTypedWithoutPrefix_ShouldNotMatchKeys()
    {
        await using var connection = await OpenDatabaseAsync();
        await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 58112, "Not asked for while typing");

        (await SearchAsync("q=58112")).ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenWordWithNoDigit_ShouldNotMatchKeys()
    {
        await using var connection = await OpenDatabaseAsync();

        (await KeysAsync("HIP")).ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenNotMember_ShouldNotFindByNumber()
    {
        await using var connection = await OpenDatabaseAsync();
        await ArrangeNumberedAsync(connection, SeedIds.Projects.Part, 49273, "In a project of someone else");

        (await KeysAsync("49273")).ShouldBeEmpty();
        (await KeysAsync("4927")).ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenItemRemoved_ShouldNotFindByNumber()
    {
        await using var connection = await OpenDatabaseAsync();
        await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 38164, "Withdrawn", SeedIds.States.Ship.StoryRemoved);

        (await KeysAsync("38164")).ShouldBeEmpty();
        (await KeysAsync("3816")).ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenItemClosedAndNumberTyped_ShouldFindItWholeButNotByPart()
    {
        await using var connection = await OpenDatabaseAsync();
        var closed = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 29057, "Finished", SeedIds.States.Ship.StoryClosed);

        (await SearchAsync("q=29057&prefix=true")).ShouldBe([closed]);
        (await SearchAsync("q=2905&prefix=true")).ShouldBeEmpty();
        (await SearchAsync("q=2905&prefix=true&closed=true")).ShouldBe([closed]);
    }

    [Test]
    public async Task Search_WhenTypesSetAndNumberTyped_ShouldKeepToThem()
    {
        await using var connection = await OpenDatabaseAsync();
        var bug = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 17365, "A bug", type: "bug");
        var story = await ArrangeNumberedAsync(connection, SeedIds.Projects.Ship, 173659, "A story");

        (await SearchAsync("q=17365&prefix=true&types=story")).ShouldBe([story]);
        (await SearchAsync("q=17365&prefix=true&types=bug")).ShouldBe([bug]);
    }

    private Task<List<string>> KeysAsync(string query) => SearchAsync($"q={Uri.EscapeDataString(query)}&prefix=true&closed=true");

    private async Task<List<string>> SearchAsync(string query)
    {
        using var response = await Api.GetAsync($"api/search?{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        return [.. JsonDocument.Parse(text).RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];
    }

    // An item at a chosen number, far above anything seeded. It has no search_doc rows, so only its key can match it.
    private static async Task<string> ArrangeNumberedAsync(NpgsqlConnection connection, Guid projectId, int number, string title, Guid? stateId = null, string type = "story")
    {
        var builder = new WorkItemBuilder(projectId).WithTitle(title).WithType(type);
        if (stateId is not null)
        {
            builder.WithState(stateId.Value);
        }

        var id = await builder.InsertAsync(connection);
        return await connection.ExecuteScalarAsync<string>(
            "UPDATE work_item SET number = @number, key = split_part(key, '-', 1) || '-' || @number WHERE id = @id RETURNING key",
            new { id, number }) ?? throw new InvalidOperationException("The arranged item is gone.");
    }
}
