using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog;

// A BAU item is made by ShipLean, so the backlog add refuses it, and refuses the whole block when one row is BAU.
[TestFixture]
public class BacklogAddBauTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog/items";

    [Test]
    public async Task Add_WhenBau_ShouldBeValidationNamingType()
    {
        var before = await CountAsync();

        using var response = await Api.PostJsonAsync(Url, Body(Row("bau", "Rota")));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("type");
        error.Error.ShouldBe("A BAU item is made by ShipLean, not by hand.");
        (await CountAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Add_WhenBauAmongValidRows_ShouldStoreNothing()
    {
        var before = await CountAsync();

        using var response = await Api.PostJsonAsync(Url, Body(Row("story", "A fine story"), Row("bau", "Rota"), Row("story", "Another fine story")));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("type");
        (await CountAsync()).ShouldBe(before);
    }

    private static object Row(string type, string title)
        => new { type, title, description = (string?)null, points = (int?)null, parentId = (Guid?)null, tasks = Array.Empty<object>() };

    private static object Body(params object[] items)
        => new { items, sprintId = (Guid?)null, placement = (object?)null };

    private async Task<int> CountAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item");
    }
}
