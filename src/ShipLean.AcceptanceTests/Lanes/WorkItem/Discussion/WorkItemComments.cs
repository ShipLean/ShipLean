using System.Text.Json;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Discussion;

// What the discussion tests read and arrange outside the page.
internal static class WorkItemComments
{
    public static async Task<JsonElement> ListAsync(IPage page, Guid itemId)
    {
        var response = await page.Context.APIRequest.GetAsync($"/api/work-items/{itemId}/comments");
        response.Ok.ShouldBeTrue();
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    // A comment written through the API as the signed-in user, before the page opens.
    public static async Task<Guid> AddAsync(IPage page, Guid itemId, string body)
    {
        var response = await page.Context.APIRequest.PostAsync($"/api/work-items/{itemId}/comments", new APIRequestContextOptions
        {
            DataObject = new { body },
        });
        response.Ok.ShouldBeTrue();
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("id").GetGuid();
    }

    // Tom, signed in on a context of his own, comments on the item behind the page's back.
    public static async Task AddAsTomAsync(IPage page, string baseUrl, Guid itemId, string body)
    {
        await using var context = await page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        var headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "teammate-context" };
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = headers,
        });
        login.Ok.ShouldBeTrue();
        var posted = await context.APIRequest.PostAsync($"/api/work-items/{itemId}/comments", new APIRequestContextOptions
        {
            DataObject = new { body },
            Headers = headers,
        });
        posted.Ok.ShouldBeTrue(await posted.TextAsync());
    }

    // Tom, signed in on a context of his own, marks the comment superseded behind the page's back.
    public static async Task SupersedeAsTomAsync(IPage page, string baseUrl, Guid commentId)
    {
        await using var context = await page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        var headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "teammate-context" };
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = headers,
        });
        login.Ok.ShouldBeTrue();
        var marked = await context.APIRequest.PostAsync($"/api/comments/{commentId}/supersede", new APIRequestContextOptions { Headers = headers });
        marked.Ok.ShouldBeTrue(await marked.TextAsync());
    }

    // The comment row as stored: its body and whether it is archived.
    public static async Task<(string Body, bool Archived)?> RowAsync(string connectionString, Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT body, archived_at IS NOT NULL FROM comment WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? (reader.GetString(0), reader.GetBoolean(1)) : null;
    }
}
